// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Configuration;
using Open.IdentityServer.Extensions;
using Open.IdentityServer.Models;
using Open.IdentityServer.Stores;
using Open.IdentityServer.Utility;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Open.IdentityServer.Services;

/// <summary>
/// Default implementation of <see cref="IServerSideTicketStore"/> that persists the
/// cookie authentication ticket in the <see cref="IServerSideSessionStore"/>.
/// The serialized ticket is protected with ASP.NET Core data protection before
/// it is written to the store, so authentication session data is encrypted at rest.
/// </summary>
public class ServerSideTicketStore : IServerSideTicketStore
{
    /// <summary>
    /// The purpose string used for the data protector that protects the
    /// serialized authentication tickets at rest.
    /// </summary>
    public const string DataProtectorPurpose = "Open.IdentityServer.ServerSideTicketStore";

    private readonly IdentityServerOptions _options;
    private readonly IServerSideSessionStore _store;
    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;
    private readonly ILogger<ServerSideTicketStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerSideTicketStore"/> class.
    /// </summary>
    /// <param name="options">The IdentityServer options.</param>
    /// <param name="store">The server-side session store.</param>
    /// <param name="dataProtectionProvider">The data protection provider used to protect tickets at rest.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public ServerSideTicketStore(
        IdentityServerOptions options,
        IServerSideSessionStore store,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider clock,
        ILogger<ServerSideTicketStore> logger)
    {
        _options = options;
        _store = store;
        _protector = dataProtectionProvider.CreateProtector(DataProtectorPurpose);
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        if (ticket == null) throw new ArgumentNullException(nameof(ticket));

        // it's possible that the user re-triggers this several times, so we
        // use a new key each time to avoid the chance of a key being reused
        // for different sessions. keys are 256 bits of CSPRNG output, so
        // they are not guessable or enumerable.
        var key = CryptoRandom.CreateUniqueId(32, CryptoRandom.OutputFormat.Base64Url);

        await CreateNewSessionAsync(key, ticket);

        return key;
    }

    /// <inheritdoc/>
    public virtual async Task<AuthenticationTicket> RetrieveAsync(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        var session = await _store.GetSessionAsync(key);
        if (session == null)
        {
            return null;
        }

        // defense in depth: the cookie handler also validates the expiration
        // contained in the ticket, but we never hand out a ticket for a
        // session that is already expired.
        if (session.Expires.HasValue && session.Expires.Value <= _clock.GetUtcNow().UtcDateTime)
        {
            _logger.LogDebug("Server-side session for subject {subjectId} and session id {sessionId} has expired. Removing.", session.SubjectId, session.SessionId);
            await _store.DeleteSessionAsync(key);
            return null;
        }

        var ticket = Unprotect(session.Ticket);
        if (ticket == null)
        {
            // if the ticket cannot be unprotected (tampered data, lost data
            // protection keys), treat the session as invalid and remove it.
            _logger.LogWarning("Failed to unprotect or deserialize server-side session ticket for subject {subjectId} and session id {sessionId}. Removing session.", session.SubjectId, session.SessionId);
            await _store.DeleteSessionAsync(key);
        }

        return ticket;
    }

    /// <inheritdoc/>
    public virtual async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (ticket == null) throw new ArgumentNullException(nameof(ticket));

        var session = await _store.GetSessionAsync(key);
        if (session == null)
        {
            // the cookie authentication handler can call renew for a key that no
            // longer exists (e.g. it was just removed by the cleanup job while the
            // request was in flight), so we re-create the session.
            // see https://github.com/dotnet/aspnetcore/issues/41516
            await CreateNewSessionAsync(key, ticket);
            return;
        }

        session.Scheme = ticket.AuthenticationScheme;
        session.SubjectId = GetRequiredSubjectId(ticket);
        session.SessionId = ticket.Properties.GetSessionId();
        session.DisplayName = GetDisplayName(ticket);
        session.Renewed = _clock.GetUtcNow().UtcDateTime;
        session.Expires = ticket.Properties.ExpiresUtc?.UtcDateTime;
        session.Ticket = Protect(ticket);

        await _store.UpdateSessionAsync(session);
    }

    /// <inheritdoc/>
    public virtual Task RemoveAsync(string key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        return _store.DeleteSessionAsync(key);
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyCollection<UserSession>> GetSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default)
    {
        var sessions = await _store.GetSessionsAsync(filter, cancellationToken);

        return AsUserSessions(sessions);
    }

    /// <inheritdoc/>
    public virtual async Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery filter = null, CancellationToken cancellationToken = default)
    {
        var results = await _store.QuerySessionsAsync(filter, cancellationToken);

        return new QueryResult<UserSession>
        {
            ResultsToken = results.ResultsToken,
            HasPrevResults = results.HasPrevResults,
            HasNextResults = results.HasNextResults,
            TotalCount = results.TotalCount,
            TotalPages = results.TotalPages,
            CurrentPage = results.CurrentPage,
            Results = AsUserSessions(results.Results)
        };
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyCollection<UserSession>> GetAndRemoveExpiredSessionsAsync(int count, CancellationToken cancellationToken = default)
    {
        var sessions = await _store.GetAndRemoveExpiredSessionsAsync(count, cancellationToken);

        return AsUserSessions(sessions);
    }

    private async Task CreateNewSessionAsync(string key, AuthenticationTicket ticket)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var session = new ServerSideSession
        {
            Key = key,
            Scheme = ticket.AuthenticationScheme,
            SubjectId = GetRequiredSubjectId(ticket),
            SessionId = ticket.Properties.GetSessionId(),
            DisplayName = GetDisplayName(ticket),
            Created = now,
            Renewed = now,
            Expires = ticket.Properties.ExpiresUtc?.UtcDateTime,
            Ticket = Protect(ticket)
        };

        await _store.CreateSessionAsync(session);
    }

    private static string GetRequiredSubjectId(AuthenticationTicket ticket)
    {
        var sub = ticket.Principal?.FindFirst(JwtClaimTypes.Subject)?.Value;
        if (String.IsNullOrWhiteSpace(sub))
        {
            throw new InvalidOperationException("Authenticated user must have a 'sub' claim in order to use server-side sessions.");
        }

        return sub;
    }

    private string GetDisplayName(AuthenticationTicket ticket)
    {
        var claimType = _options.ServerSideSessions.UserDisplayNameClaimType;
        if (claimType == null) return null;

        var name = ticket.Principal?.FindFirst(claimType)?.Value;

        // keep within the column length used by relational stores
        if (name?.Length > 100)
        {
            name = name.Substring(0, 100);
        }

        return name;
    }

    private string Protect(AuthenticationTicket ticket)
    {
        var payload = TicketSerializer.Default.Serialize(ticket);
        return Convert.ToBase64String(_protector.Protect(payload));
    }

    private AuthenticationTicket Unprotect(string data)
    {
        if (String.IsNullOrWhiteSpace(data)) return null;

        try
        {
            var payload = _protector.Unprotect(Convert.FromBase64String(data));
            return TicketSerializer.Default.Deserialize(payload);
        }
        catch (Exception ex)
        {
            // do not leak ticket contents; a failure here means the data was
            // tampered with or the data protection keys are no longer available
            _logger.LogDebug("Failed to unprotect server-side session ticket: {error}", ex.Message);
            return null;
        }
    }

    private IReadOnlyCollection<UserSession> AsUserSessions(IEnumerable<ServerSideSession> sessions)
    {
        var results = new List<UserSession>();

        foreach (var session in sessions)
        {
            var ticket = Unprotect(session.Ticket);
            if (ticket == null)
            {
                _logger.LogWarning("Failed to unprotect or deserialize server-side session ticket for subject {subjectId} and session id {sessionId}. Skipping.", session.SubjectId, session.SessionId);
                continue;
            }

            results.Add(new UserSession
            {
                SubjectId = session.SubjectId,
                SessionId = session.SessionId,
                DisplayName = session.DisplayName,
                Created = session.Created,
                Renewed = session.Renewed,
                Expires = session.Expires,
                ClientIds = ticket.Properties.GetClientList().ToArray(),
                Ticket = ticket
            });
        }

        return results;
    }
}

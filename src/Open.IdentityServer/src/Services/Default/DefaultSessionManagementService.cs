// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Extensions;
using Open.IdentityServer.Models;
using Open.IdentityServer.Stores;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Open.IdentityServer.Services;

/// <summary>
/// Default implementation of <see cref="ISessionManagementService"/>.
/// </summary>
public class DefaultSessionManagementService : ISessionManagementService
{
    private readonly IServerSideTicketStore _ticketStore;
    private readonly IServerSideSessionStore _sessionStore;
    private readonly IPersistedGrantStore _persistedGrantStore;
    private readonly IBackChannelLogoutService _backChannelLogoutService;
    private readonly ILogger<DefaultSessionManagementService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSessionManagementService"/> class.
    /// </summary>
    /// <param name="ticketStore">The server-side ticket store.</param>
    /// <param name="sessionStore">The server-side session store.</param>
    /// <param name="persistedGrantStore">The persisted grant store.</param>
    /// <param name="backChannelLogoutService">The back-channel logout service.</param>
    /// <param name="logger">The logger.</param>
    public DefaultSessionManagementService(
        IServerSideTicketStore ticketStore,
        IServerSideSessionStore sessionStore,
        IPersistedGrantStore persistedGrantStore,
        IBackChannelLogoutService backChannelLogoutService,
        ILogger<DefaultSessionManagementService> logger)
    {
        _ticketStore = ticketStore;
        _sessionStore = sessionStore;
        _persistedGrantStore = persistedGrantStore;
        _backChannelLogoutService = backChannelLogoutService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery filter = null, CancellationToken cancellationToken = default)
    {
        return _ticketStore.QuerySessionsAsync(filter, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveSessionsAsync(RemoveSessionsContext context, CancellationToken cancellationToken = default)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var filter = new SessionFilter
        {
            SubjectId = context.SubjectId,
            SessionId = context.SessionId
        };
        // throws when neither subject id nor session id is provided, so a
        // misconfigured call can never remove or revoke every session
        filter.Validate();

        if (context.SendBackchannelLogoutNotification)
        {
            // load the sessions before they are removed so the client list is still available
            var sessions = await _ticketStore.GetSessionsAsync(filter, cancellationToken);

            foreach (var session in sessions)
            {
                var clientIds = FilterClientIds(session.ClientIds, context.ClientIds);
                if (clientIds.Count == 0) continue;

                _logger.LogDebug("Sending back-channel logout notifications for subject {subjectId} and session id {sessionId}", session.SubjectId, session.SessionId);

                await _backChannelLogoutService.SendLogoutNotificationsAsync(new LogoutNotificationContext
                {
                    SubjectId = session.SubjectId,
                    SessionId = session.SessionId,
                    ClientIds = clientIds
                });
            }
        }

        if (context.RevokeTokens || context.RevokeConsents)
        {
            await RevokeGrantsAsync(context, cancellationToken);
        }

        if (context.RemoveServerSideSession)
        {
            _logger.LogDebug("Removing server-side sessions for subject {subjectId} and session id {sessionId}", context.SubjectId, context.SessionId);

            await _sessionStore.DeleteSessionsAsync(filter, cancellationToken);
        }
    }

    private async Task RevokeGrantsAsync(RemoveSessionsContext context, CancellationToken cancellationToken)
    {
        var clientIds = context.ClientIds?.ToArray();

        // the persisted grant filter supports a single client id, so iterate when
        // specific clients are requested; null means all clients
        var clientFilters = (clientIds == null || clientIds.Length == 0)
            ? new string[] { null }
            : clientIds;

        foreach (var clientId in clientFilters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (context.RevokeTokens)
            {
                _logger.LogDebug("Revoking tokens for subject {subjectId} and session id {sessionId}", context.SubjectId, context.SessionId);

                // tokens issued in the session carry the session id, so this removes all
                // grants bound to the session (refresh tokens, reference tokens, codes)
                await _persistedGrantStore.RemoveAllAsync(new PersistedGrantFilter
                {
                    SubjectId = context.SubjectId,
                    SessionId = context.SessionId,
                    ClientId = clientId
                });
            }

            if (context.RevokeConsents && context.SubjectId != null)
            {
                _logger.LogDebug("Revoking consents for subject {subjectId}", context.SubjectId);

                // consents are not session bound, so they are only filtered by subject (and client)
                await _persistedGrantStore.RemoveAllAsync(new PersistedGrantFilter
                {
                    SubjectId = context.SubjectId,
                    ClientId = clientId,
                    Type = IdentityServerConstants.PersistedGrantTypes.UserConsent
                });
            }
        }
    }

    private static IReadOnlyCollection<string> FilterClientIds(IReadOnlyCollection<string> sessionClientIds, IEnumerable<string> requestedClientIds)
    {
        if (requestedClientIds == null) return sessionClientIds;

        return sessionClientIds.Intersect(requestedClientIds).ToArray();
    }
}

// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Open.IdentityServer.EntityFramework.Interfaces;
using Open.IdentityServer.EntityFramework.Mappers;
using Open.IdentityServer.Extensions;
using Open.IdentityServer.Models;
using Open.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Open.IdentityServer.EntityFramework.Stores;

/// <summary>
/// Implementation of IServerSideSessionStore that uses EF.
/// </summary>
/// <seealso cref="Open.IdentityServer.Stores.IServerSideSessionStore" />
public class ServerSideSessionStore : IServerSideSessionStore
{
    /// <summary>
    /// The default page size used by <see cref="QuerySessionsAsync"/> when no count is requested.
    /// </summary>
    public const int DefaultQueryCount = 25;

    /// <summary>
    /// The DbContext.
    /// </summary>
    protected readonly IPersistedGrantDbContext Context;

    /// <summary>
    /// The clock.
    /// </summary>
    protected readonly TimeProvider Clock;

    /// <summary>
    /// The logger.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerSideSessionStore"/> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public ServerSideSessionStore(IPersistedGrantDbContext context, TimeProvider clock, ILogger<ServerSideSessionStore> logger)
    {
        Context = context;
        Clock = clock;
        Logger = logger;
    }

    /// <inheritdoc/>
    public virtual async Task CreateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        Context.ServerSideSessions.Add(session.ToEntity());

        try
        {
            await Context.SaveChangesAsync();

            Logger.LogDebug("Created server-side session for subject {subjectId} and session id {sessionId} in database", session.SubjectId, session.SessionId);
        }
        catch (DbUpdateException ex)
        {
            Logger.LogWarning("Exception creating server-side session in database: {error}", ex.Message);
            throw;
        }
    }

    /// <inheritdoc/>
    public virtual async Task<ServerSideSession> GetSessionAsync(string key, CancellationToken cancellationToken = default)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        var entity = (await Context.ServerSideSessions.AsNoTracking()
                .Where(x => x.Key == key)
                .ToArrayAsync(cancellationToken))
            .SingleOrDefault(x => x.Key == key);

        Logger.LogDebug("Server-side session found in database: {found}", entity != null);

        return entity?.ToModel();
    }

    /// <inheritdoc/>
    public virtual async Task UpdateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        var entity = (await Context.ServerSideSessions
                .Where(x => x.Key == session.Key)
                .ToArrayAsync(cancellationToken))
            .SingleOrDefault(x => x.Key == session.Key);

        if (entity == null)
        {
            Logger.LogDebug("No server-side session found in database to update");
            return;
        }

        session.UpdateEntity(entity);

        try
        {
            await Context.SaveChangesAsync();

            Logger.LogDebug("Updated server-side session for subject {subjectId} and session id {sessionId} in database", session.SubjectId, session.SessionId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogWarning("Exception updating server-side session in database: {error}", ex.Message);
        }
    }

    /// <inheritdoc/>
    public virtual async Task DeleteSessionAsync(string key, CancellationToken cancellationToken = default)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        var entity = (await Context.ServerSideSessions
                .Where(x => x.Key == key)
                .ToArrayAsync(cancellationToken))
            .SingleOrDefault(x => x.Key == key);

        if (entity == null)
        {
            Logger.LogDebug("No server-side session found in database to delete");
            return;
        }

        Context.ServerSideSessions.Remove(entity);

        try
        {
            await Context.SaveChangesAsync();

            Logger.LogDebug("Deleted server-side session for subject {subjectId} and session id {sessionId} in database", entity.SubjectId, entity.SessionId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogInformation("Exception deleting server-side session in database: {error}", ex.Message);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default)
    {
        filter.Validate();

        var entities = await Filter(Context.ServerSideSessions.AsNoTracking().AsQueryable(), filter)
            .ToArrayAsync(cancellationToken);

        Logger.LogDebug("{count} server-side sessions found for {@filter}", entities.Length, filter);

        return entities.Select(x => x.ToModel()).ToArray();
    }

    /// <inheritdoc/>
    public virtual async Task DeleteSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default)
    {
        filter.Validate();

        var entities = await Filter(Context.ServerSideSessions.AsQueryable(), filter)
            .ToArrayAsync(cancellationToken);

        Logger.LogDebug("Removing {count} server-side sessions from database for {@filter}", entities.Length, filter);

        Context.ServerSideSessions.RemoveRange(entities);

        try
        {
            await Context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Logger.LogInformation("Exception removing {count} server-side sessions from database for {@filter}: {error}", entities.Length, filter, ex.Message);
        }
    }

    /// <inheritdoc/>
    public virtual async Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, CancellationToken cancellationToken = default)
    {
        var now = Clock.GetUtcNow().UtcDateTime;

        var entities = await Context.ServerSideSessions
            .Where(x => x.Expires != null && x.Expires <= now)
            .OrderBy(x => x.Id)
            .Take(count)
            .ToArrayAsync(cancellationToken);

        if (entities.Length == 0)
        {
            return Array.Empty<ServerSideSession>();
        }

        Context.ServerSideSessions.RemoveRange(entities);

        try
        {
            await Context.SaveChangesAsync();

            Logger.LogDebug("Removed {count} expired server-side sessions from database", entities.Length);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // another instance removed (some of) the same sessions; report none removed so
            // downstream notifications (e.g. back-channel logout) are not sent twice
            Logger.LogDebug("Concurrency exception removing expired server-side sessions: {error}", ex.Message);

            return Array.Empty<ServerSideSession>();
        }

        return entities.Select(x => x.ToModel()).ToArray();
    }

    /// <inheritdoc/>
    public virtual async Task<QueryResult<ServerSideSession>> QuerySessionsAsync(SessionQuery filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new SessionQuery();

        var countRequested = filter.CountRequested;
        if (countRequested <= 0) countRequested = DefaultQueryCount;

        var query = ApplyQueryFilter(Context.ServerSideSessions.AsNoTracking().AsQueryable(), filter);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Max(1, Math.Ceiling(totalCount / (double)countRequested));

        Entities.IdentityServerServerSideSessions[] entities;
        if (PaginationToken.TryParse(filter.ResultsToken, out var token))
        {
            if (filter.RequestPriorResults)
            {
                entities = (await query
                        .Where(x => x.Id < token.First)
                        .OrderByDescending(x => x.Id)
                        .Take(countRequested)
                        .ToArrayAsync(cancellationToken))
                    .OrderBy(x => x.Id)
                    .ToArray();
            }
            else
            {
                entities = await query
                    .Where(x => x.Id > token.Last)
                    .OrderBy(x => x.Id)
                    .Take(countRequested)
                    .ToArrayAsync(cancellationToken);
            }
        }
        else
        {
            entities = await query
                .OrderBy(x => x.Id)
                .Take(countRequested)
                .ToArrayAsync(cancellationToken);
        }

        var result = new QueryResult<ServerSideSession>
        {
            Results = entities.Select(x => x.ToModel()).ToArray(),
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        if (entities.Length > 0)
        {
            var first = entities[0].Id;
            var last = entities[entities.Length - 1].Id;

            result.ResultsToken = new PaginationToken(first, last).ToString();
            result.HasPrevResults = await query.AnyAsync(x => x.Id < first, cancellationToken);
            result.HasNextResults = await query.AnyAsync(x => x.Id > last, cancellationToken);
        }

        Logger.LogDebug("Server-side sessions queried from database: {count} of total {totalCount}", entities.Length, totalCount);

        return result;
    }

    private static IQueryable<Entities.IdentityServerServerSideSessions> Filter(
        IQueryable<Entities.IdentityServerServerSideSessions> query, SessionFilter filter)
    {
        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }
        if (!String.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }

        return query;
    }

    private static IQueryable<Entities.IdentityServerServerSideSessions> ApplyQueryFilter(
        IQueryable<Entities.IdentityServerServerSideSessions> query, SessionQuery filter)
    {
        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.SubjectId == filter.SubjectId);
        }
        if (!String.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.SessionId == filter.SessionId);
        }
        if (!String.IsNullOrWhiteSpace(filter.DisplayName))
        {
            query = query.Where(x => x.DisplayName != null && x.DisplayName.Contains(filter.DisplayName));
        }

        return query;
    }
}

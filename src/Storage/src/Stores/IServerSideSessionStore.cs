// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Open.IdentityServer.Stores;

/// <summary>
/// Interface to model storage of server-side sessions.
/// </summary>
public interface IServerSideSessionStore
{
    /// <summary>
    /// Creates a server-side session.
    /// </summary>
    /// <param name="session">The session to create.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that completes when the session has been created.</returns>
    Task CreateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a server-side session by its key.
    /// </summary>
    /// <param name="key">The key of the session.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that resolves to the session, or <c>null</c> if no session was found for the key.</returns>
    Task<ServerSideSession> GetSessionAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a server-side session.
    /// </summary>
    /// <param name="session">The session to update.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that completes when the session has been updated.</returns>
    Task UpdateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a server-side session by its key.
    /// </summary>
    /// <param name="key">The key of the session.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that completes when the session has been deleted.</returns>
    Task DeleteSessionAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all server-side sessions matching the filter.
    /// </summary>
    /// <param name="filter">The filter. At least one filter value must be supplied.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that resolves to the matching sessions.</returns>
    Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all server-side sessions matching the filter.
    /// </summary>
    /// <param name="filter">The filter. At least one filter value must be supplied.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that completes when the sessions have been deleted.</returns>
    Task DeleteSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and removes expired server-side sessions.
    /// </summary>
    /// <param name="count">The maximum number of expired sessions to remove.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that resolves to the sessions that were removed.</returns>
    Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries server-side sessions with paging support.
    /// </summary>
    /// <param name="filter">The query filter.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that resolves to a page of matching sessions.</returns>
    Task<QueryResult<ServerSideSession>> QuerySessionsAsync(SessionQuery filter = null, CancellationToken cancellationToken = default);
}

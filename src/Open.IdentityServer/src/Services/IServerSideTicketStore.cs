// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Models;
using Open.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Open.IdentityServer.Services;

/// <summary>
/// A per-request service that acts as the cookie authentication handler's
/// <see cref="ITicketStore"/> backed by the <see cref="IServerSideSessionStore"/>,
/// and provides access to the user sessions it manages.
/// </summary>
public interface IServerSideTicketStore : ITicketStore
{
    /// <summary>
    /// Gets all user sessions matching the filter.
    /// </summary>
    /// <param name="filter">The filter. At least one filter value must be supplied.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that resolves to the matching user sessions.</returns>
    Task<IReadOnlyCollection<UserSession>> GetSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries user sessions with paging support.
    /// </summary>
    /// <param name="filter">The query filter.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that resolves to a page of matching user sessions.</returns>
    Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves and removes expired sessions.
    /// </summary>
    /// <param name="count">The maximum number of expired sessions to remove.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that resolves to the user sessions that were removed.</returns>
    Task<IReadOnlyCollection<UserSession>> GetAndRemoveExpiredSessionsAsync(int count, CancellationToken cancellationToken = default);
}

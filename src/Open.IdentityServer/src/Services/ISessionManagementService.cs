// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Models;
using Open.IdentityServer.Stores;
using System.Threading;
using System.Threading.Tasks;

namespace Open.IdentityServer.Services;

/// <summary>
/// Service for querying and terminating a user's server-side sessions.
/// </summary>
public interface ISessionManagementService
{
    /// <summary>
    /// Queries user sessions with paging support.
    /// </summary>
    /// <param name="filter">The query filter.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that resolves to a page of matching user sessions.</returns>
    Task<QueryResult<UserSession>> QuerySessionsAsync(SessionQuery filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes user sessions and optionally revokes the associated tokens and consents,
    /// and sends back-channel logout notifications to the clients of the sessions.
    /// </summary>
    /// <param name="context">The context describing which sessions to remove and what additional cleanup to perform.</param>
    /// <param name="cancellationToken">A token that propagates notification that the operation should be canceled.</param>
    /// <returns>A task that completes when the sessions have been removed.</returns>
    Task RemoveSessionsAsync(RemoveSessionsContext context, CancellationToken cancellationToken = default);
}

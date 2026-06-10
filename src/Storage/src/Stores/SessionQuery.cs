// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Open.IdentityServer.Stores;

/// <summary>
/// Represents a query against the server-side session store.
/// Setting multiple filter properties is interpreted as a logical 'AND' to further filter the query.
/// </summary>
public class SessionQuery
{
    /// <summary>
    /// The token indicating the prior results returned, used to continue paging.
    /// Obtained from the <c>ResultsToken</c> of a prior query result.
    /// If <c>null</c>, the first page of results is returned.
    /// </summary>
    public string ResultsToken { get; set; }

    /// <summary>
    /// If <c>true</c>, requests the page of results prior to the results
    /// indicated by the <see cref="ResultsToken"/>. If <c>false</c>, the
    /// next page of results is requested.
    /// </summary>
    public bool RequestPriorResults { get; set; }

    /// <summary>
    /// The number of results requested per page. If 0 or negative, a default
    /// is used by the store implementation.
    /// </summary>
    public int CountRequested { get; set; }

    /// <summary>
    /// The subject id of the user to filter on (exact match).
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// The session id to filter on (exact match).
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The user's display name to filter on (contains match).
    /// </summary>
    public string DisplayName { get; set; }
}

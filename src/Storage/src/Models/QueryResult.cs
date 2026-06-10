// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Open.IdentityServer.Models;

/// <summary>
/// A page of results from a paged query.
/// </summary>
/// <typeparam name="T">The type of the items returned by the query.</typeparam>
public class QueryResult<T>
{
    /// <summary>
    /// Gets or sets the token that indicates the prior results returned.
    /// Pass this on a subsequent query to request the next (or prior) page of results.
    /// </summary>
    /// <value>
    /// The results token.
    /// </value>
    public string ResultsToken { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there are results prior to the current page.
    /// </summary>
    /// <value>
    /// <c>true</c> if there are prior results; otherwise, <c>false</c>.
    /// </value>
    public bool HasPrevResults { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether there are results after the current page.
    /// </summary>
    /// <value>
    /// <c>true</c> if there are more results; otherwise, <c>false</c>.
    /// </value>
    public bool HasNextResults { get; set; }

    /// <summary>
    /// Gets or sets the total count of items matched by the query.
    /// </summary>
    /// <value>
    /// The total count.
    /// </value>
    public int? TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of pages matched by the query.
    /// </summary>
    /// <value>
    /// The total number of pages.
    /// </value>
    public int? TotalPages { get; set; }

    /// <summary>
    /// Gets or sets the current page number.
    /// </summary>
    /// <value>
    /// The current page number.
    /// </value>
    public int? CurrentPage { get; set; }

    /// <summary>
    /// Gets or sets the items for the current page.
    /// </summary>
    /// <value>
    /// The items.
    /// </value>
    public IReadOnlyCollection<T> Results { get; set; }
}

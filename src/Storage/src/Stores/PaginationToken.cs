// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Globalization;

namespace Open.IdentityServer.Stores;

/// <summary>
/// Models the value of the results token used by <see cref="IServerSideSessionStore.QuerySessionsAsync"/>
/// to perform keyset pagination. The token captures the position of the first and last item
/// of the page that was returned.
/// </summary>
public readonly struct PaginationToken
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PaginationToken"/> struct.
    /// </summary>
    /// <param name="first">The position of the first item of the page.</param>
    /// <param name="last">The position of the last item of the page.</param>
    public PaginationToken(long first, long last)
    {
        First = first;
        Last = last;
    }

    /// <summary>
    /// The position of the first item of the page.
    /// </summary>
    public long First { get; }

    /// <summary>
    /// The position of the last item of the page.
    /// </summary>
    public long Last { get; }

    /// <summary>
    /// Parses a results token. Malformed or empty tokens are rejected, in which
    /// case callers are expected to return the first page of results.
    /// </summary>
    /// <param name="value">The token value to parse.</param>
    /// <param name="token">The parsed token.</param>
    /// <returns><c>true</c> if the token was parsed successfully; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string value, out PaginationToken token)
    {
        token = default;

        if (String.IsNullOrWhiteSpace(value)) return false;

        var parts = value.Split(':');
        if (parts.Length != 2) return false;

        if (!Int64.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var first)) return false;
        if (!Int64.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var last)) return false;

        token = new PaginationToken(first, last);
        return true;
    }

    /// <summary>
    /// Returns the string representation of the token.
    /// </summary>
    /// <returns>The token value.</returns>
    public override string ToString()
    {
        return FormattableString.Invariant($"{First}:{Last}");
    }
}

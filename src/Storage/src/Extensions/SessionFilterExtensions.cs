// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Stores;
using System;

namespace Open.IdentityServer.Extensions;

/// <summary>
/// Extensions for SessionFilter.
/// </summary>
public static class SessionFilterExtensions
{
    /// <summary>
    /// Validates the SessionFilter and throws if invalid. This prevents
    /// accidentally operating on every session in the store when no filter
    /// values have been supplied.
    /// </summary>
    /// <param name="filter">The filter to validate.</param>
    /// <exception cref="ArgumentNullException">When <paramref name="filter"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">When no filter values are set.</exception>
    public static void Validate(this SessionFilter filter)
    {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        if (String.IsNullOrWhiteSpace(filter.SubjectId) &&
            String.IsNullOrWhiteSpace(filter.SessionId))
        {
            throw new ArgumentException("No filter values set.", nameof(filter));
        }
    }
}

// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

namespace Open.IdentityServer.Stores;

/// <summary>
/// Represents a filter used when accessing the server-side session store.
/// Setting multiple properties is interpreted as a logical 'AND' to further filter the query.
/// At least one value must be supplied.
/// </summary>
public class SessionFilter
{
    /// <summary>
    /// Subject id of the user.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Session id of the user's authentication session.
    /// </summary>
    public string SessionId { get; set; }
}

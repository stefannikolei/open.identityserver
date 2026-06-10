// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace Open.IdentityServer.Models;

/// <summary>
/// A model for a user's authentication session stored server-side.
/// </summary>
public class ServerSideSession
{
    /// <summary>
    /// Gets or sets the key. This is the handle that the authentication cookie
    /// uses to reference the session and must be unique.
    /// </summary>
    /// <value>
    /// The key.
    /// </value>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the authentication scheme the session was issued for.
    /// </summary>
    /// <value>
    /// The scheme.
    /// </value>
    public string Scheme { get; set; }

    /// <summary>
    /// Gets or sets the subject id of the user the session belongs to.
    /// </summary>
    /// <value>
    /// The subject id.
    /// </value>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the session id issued for the user's authentication session.
    /// </summary>
    /// <value>
    /// The session id.
    /// </value>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user.
    /// </summary>
    /// <value>
    /// The display name.
    /// </value>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the creation time of the session.
    /// </summary>
    /// <value>
    /// The creation time.
    /// </value>
    public DateTime Created { get; set; }

    /// <summary>
    /// Gets or sets the time the session was last renewed.
    /// </summary>
    /// <value>
    /// The renewal time.
    /// </value>
    public DateTime Renewed { get; set; }

    /// <summary>
    /// Gets or sets the expiration time of the session.
    /// </summary>
    /// <value>
    /// The expiration time.
    /// </value>
    public DateTime? Expires { get; set; }

    /// <summary>
    /// Gets or sets the serialized and protected authentication ticket of the session.
    /// </summary>
    /// <value>
    /// The ticket.
    /// </value>
    public string Ticket { get; set; }
}

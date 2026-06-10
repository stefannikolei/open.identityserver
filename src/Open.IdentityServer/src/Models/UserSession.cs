// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;

namespace Open.IdentityServer.Models;

/// <summary>
/// Models a user's authentication session backed by a server-side session.
/// </summary>
public class UserSession
{
    /// <summary>
    /// Gets or sets the subject id of the user.
    /// </summary>
    /// <value>
    /// The subject id.
    /// </value>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the session id of the user's authentication session.
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
    /// Gets or sets the ids of the clients the user has signed in to during the session.
    /// </summary>
    /// <value>
    /// The client ids.
    /// </value>
    public IReadOnlyCollection<string> ClientIds { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the authentication ticket of the session.
    /// </summary>
    /// <value>
    /// The authentication ticket.
    /// </value>
    public Microsoft.AspNetCore.Authentication.AuthenticationTicket Ticket { get; set; }
}

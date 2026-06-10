// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Open.IdentityServer.Models;

/// <summary>
/// Models the information needed to remove a user's server-side sessions.
/// </summary>
public class RemoveSessionsContext
{
    /// <summary>
    /// The subject id of the user whose sessions are to be removed.
    /// Either this or <see cref="SessionId"/> (or both) must be set.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// The session id of the session to remove.
    /// Either this or <see cref="SubjectId"/> (or both) must be set.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The client ids for which tokens and consents are revoked and back-channel logout
    /// notifications are sent. If <c>null</c> (the default), all clients of the matching
    /// sessions are affected.
    /// </summary>
    public IEnumerable<string> ClientIds { get; set; }

    /// <summary>
    /// Specifies if the server-side sessions themselves are removed,
    /// which terminates the user's authentication session. Defaults to <c>true</c>.
    /// </summary>
    public bool RemoveServerSideSession { get; set; } = true;

    /// <summary>
    /// Specifies if the tokens and grants (refresh tokens, reference tokens,
    /// authorization codes) issued for the sessions are revoked. Defaults to <c>true</c>.
    /// </summary>
    public bool RevokeTokens { get; set; } = true;

    /// <summary>
    /// Specifies if the user's consents are revoked. Defaults to <c>true</c>.
    /// </summary>
    public bool RevokeConsents { get; set; } = true;

    /// <summary>
    /// Specifies if back-channel logout notifications are sent to the clients
    /// the user signed in to during the sessions. Defaults to <c>true</c>.
    /// </summary>
    public bool SendBackchannelLogoutNotification { get; set; } = true;
}

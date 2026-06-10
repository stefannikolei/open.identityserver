// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;

namespace Open.IdentityServer.Configuration;

/// <summary>
/// Configures behavior of server-side sessions.
/// </summary>
public class ServerSideSessionOptions
{
    /// <summary>
    /// The claim type used for the user's display name. If set, the value of this claim
    /// is copied into the server-side session record so sessions can be queried by
    /// display name. Defaults to <c>null</c> (no display name is stored).
    /// </summary>
    public string UserDisplayNameClaimType { get; set; }

    /// <summary>
    /// If enabled, expired sessions will be removed from the server-side session store
    /// by a periodic background task. Defaults to <c>true</c>.
    /// </summary>
    public bool RemoveExpiredSessions { get; set; } = true;

    /// <summary>
    /// If enabled, when expired sessions are removed, back-channel logout notifications
    /// will be sent to the clients the user had signed in to during the session.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool ExpiredSessionsTriggerBackchannelLogout { get; set; } = false;

    /// <summary>
    /// The frequency at which expired sessions will be removed. Defaults to 10 minutes.
    /// </summary>
    public TimeSpan RemoveExpiredSessionsFrequency { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// If enabled, the start of the periodic removal of expired sessions will be delayed
    /// by a random amount of time (up to the value of <see cref="RemoveExpiredSessionsFrequency"/>).
    /// This reduces contention when multiple server instances start at the same time.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool FuzzExpiredSessionRemovalStart { get; set; } = true;

    /// <summary>
    /// The maximum number of expired sessions removed per batch. Defaults to 100.
    /// </summary>
    public int RemoveExpiredSessionsBatchSize { get; set; } = 100;
}

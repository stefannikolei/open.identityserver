.. _refServerSideSessions:
Server-Side Sessions
====================
By default, Open.IdentityServer keeps the user's authentication session entirely in the
authentication cookie.
Server-side sessions move the contents of the authentication ticket out of the cookie and
into a server-side store, leaving only an opaque, cryptographically random handle in the cookie.

This enables several features:

* querying the active sessions of your users (e.g. for an administrative UI)
* terminating sessions from the server (revoking the session, the tokens issued during the session, and the user's consents)
* sending back-channel logout notifications when a session is terminated or expires
* keeping the size of the authentication cookie small

Enabling server-side sessions
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
Use the ``AddServerSideSessions`` extension on the IdentityServer builder::

    services.AddIdentityServer()
        .AddServerSideSessions();

By default an in-memory session store is used.
This is not suitable for production deployments with more than one instance,
since the sessions would not be shared across instances (and are lost on restart).

When using the Entity Framework Core operational store, the EF-based session store is
registered automatically by ``AddOperationalStore``, and sessions are persisted in the
``ServerSideSessions`` table::

    services.AddIdentityServer()
        .AddOperationalStore(options => { /* ... */ })
        .AddServerSideSessions();

Alternatively you can provide your own store implementation::

    services.AddIdentityServer()
        .AddServerSideSessions<YourSessionStore>();

Security
^^^^^^^^
The session handle stored in the cookie is 256 bits of cryptographically secure random data.
The serialized authentication ticket is protected with ASP.NET Core Data Protection before
it is written to the store, so session data is encrypted and integrity protected at rest.
If a stored ticket cannot be unprotected (e.g. it was tampered with), the session is treated
as invalid and removed.

When deploying multiple instances, the data protection keys must be shared between the
instances (as is required for cookies in general).

Options
^^^^^^^
Server-side session behavior is configured on the ``ServerSideSessions`` property of the
``IdentityServerOptions``:

``UserDisplayNameClaimType``
    The claim type used to populate the display name of the session records, so sessions
    can be queried by a user's display name. Defaults to ``null`` (no display name stored).
``RemoveExpiredSessions``
    Enables the periodic background cleanup of expired sessions. Defaults to ``true``.
``ExpiredSessionsTriggerBackchannelLogout``
    When expired sessions are removed, send back-channel logout notifications to the
    clients the user signed in to during the session. Defaults to ``false``.
``RemoveExpiredSessionsFrequency``
    The frequency of the cleanup job. Defaults to 10 minutes.
``RemoveExpiredSessionsBatchSize``
    The number of expired sessions removed per batch. Defaults to 100.
``FuzzExpiredSessionRemovalStart``
    Delays the first cleanup run by a random amount of time to reduce contention when
    several instances start simultaneously. Defaults to ``true``.

Session management
^^^^^^^^^^^^^^^^^^
The ``ISessionManagementService`` provides access to the server-side sessions:

``QuerySessionsAsync``
    Returns a paged list of sessions, optionally filtered by subject id, session id, or display name.
``RemoveSessionsAsync``
    Terminates sessions matching the subject id and/or session id of the ``RemoveSessionsContext``.
    In addition to removing the server-side session (which logs the user out), it can also
    revoke the tokens issued during the session, revoke the user's consents, and send
    back-channel logout notifications to the clients of the session.

For example, to terminate all sessions of a user and notify the clients::

    await sessionManagementService.RemoveSessionsAsync(new RemoveSessionsContext
    {
        SubjectId = "12345"
    });

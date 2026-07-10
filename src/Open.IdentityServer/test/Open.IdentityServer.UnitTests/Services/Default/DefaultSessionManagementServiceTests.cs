// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer;
using Open.IdentityServer.Configuration;
using Open.IdentityServer.Extensions;
using Open.IdentityServer.Models;
using Open.IdentityServer.Services;
using Open.IdentityServer.Stores;
using Xunit;
using AwesomeAssertions;
using IdentityServer.UnitTests.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Time.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IdentityServer.UnitTests.Services.Default;

public class DefaultSessionManagementServiceTests
{
    private class MockBackChannelLogoutService : IBackChannelLogoutService
    {
        public List<LogoutNotificationContext> SendLogoutNotificationsCalls { get; } = new List<LogoutNotificationContext>();

        public Task SendLogoutNotificationsAsync(LogoutNotificationContext context)
        {
            SendLogoutNotificationsCalls.Add(context);
            return Task.CompletedTask;
        }
    }

    private readonly FakeTimeProvider _clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly InMemoryServerSideSessionStore _sessionStore;
    private readonly InMemoryPersistedGrantStore _grantStore = new InMemoryPersistedGrantStore();
    private readonly MockBackChannelLogoutService _backChannelLogoutService = new MockBackChannelLogoutService();
    private readonly ServerSideTicketStore _ticketStore;
    private readonly DefaultSessionManagementService _subject;

    public DefaultSessionManagementServiceTests()
    {
        _sessionStore = new InMemoryServerSideSessionStore(_clock);
        _ticketStore = new ServerSideTicketStore(
            new IdentityServerOptions(),
            _sessionStore,
            new EphemeralDataProtectionProvider(),
            _clock,
            TestLogger.Create<ServerSideTicketStore>());
        _subject = new DefaultSessionManagementService(
            _ticketStore,
            _sessionStore,
            _grantStore,
            _backChannelLogoutService,
            TestLogger.Create<DefaultSessionManagementService>());
    }

    private async Task<string> CreateSessionAsync(string sub, string sid, params string[] clientIds)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(JwtClaimTypes.Subject, sub) }, "pwd"));
        var properties = new AuthenticationProperties();
        properties.SetSessionId(sid);
        foreach (var clientId in clientIds)
        {
            properties.AddClientId(clientId);
        }

        return await _ticketStore.StoreAsync(new AuthenticationTicket(principal, properties, "idsrv"));
    }

    [Fact]
    public async Task RemoveSessions_without_filter_should_fail()
    {
        Func<Task> act = () => _subject.RemoveSessionsAsync(new RemoveSessionsContext());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RemoveSessions_should_remove_server_side_sessions()
    {
        var key1 = await CreateSessionAsync("sub1", "sid1");
        var key2 = await CreateSessionAsync("sub2", "sid2");

        await _subject.RemoveSessionsAsync(new RemoveSessionsContext { SubjectId = "sub1" });

        (await _sessionStore.GetSessionAsync(key1)).Should().BeNull();
        (await _sessionStore.GetSessionAsync(key2)).Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveSessions_should_send_backchannel_logout_notifications()
    {
        await CreateSessionAsync("sub1", "sid1", "client1", "client2");

        await _subject.RemoveSessionsAsync(new RemoveSessionsContext { SubjectId = "sub1" });

        _backChannelLogoutService.SendLogoutNotificationsCalls.Count.Should().Be(1);
        var context = _backChannelLogoutService.SendLogoutNotificationsCalls.Single();
        context.SubjectId.Should().Be("sub1");
        context.SessionId.Should().Be("sid1");
        context.ClientIds.Should().BeEquivalentTo(new[] { "client1", "client2" });
    }

    [Fact]
    public async Task RemoveSessions_should_filter_backchannel_logout_clients()
    {
        await CreateSessionAsync("sub1", "sid1", "client1", "client2");

        await _subject.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "sub1",
            ClientIds = new[] { "client2" }
        });

        var context = _backChannelLogoutService.SendLogoutNotificationsCalls.Single();
        context.ClientIds.Should().BeEquivalentTo(new[] { "client2" });
    }

    [Fact]
    public async Task RemoveSessions_should_not_send_notifications_when_disabled()
    {
        await CreateSessionAsync("sub1", "sid1", "client1");

        await _subject.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "sub1",
            SendBackchannelLogoutNotification = false
        });

        _backChannelLogoutService.SendLogoutNotificationsCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveSessions_should_revoke_tokens_bound_to_session()
    {
        await CreateSessionAsync("sub1", "sid1", "client1");

        await _grantStore.StoreAsync(new PersistedGrant { Key = "rt1", Type = "refresh_token", SubjectId = "sub1", SessionId = "sid1", ClientId = "client1" });
        await _grantStore.StoreAsync(new PersistedGrant { Key = "rt2", Type = "refresh_token", SubjectId = "sub1", SessionId = "other_sid", ClientId = "client1" });

        await _subject.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "sub1",
            SessionId = "sid1",
            RevokeConsents = false,
            SendBackchannelLogoutNotification = false
        });

        (await _grantStore.GetAsync("rt1")).Should().BeNull();
        (await _grantStore.GetAsync("rt2")).Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveSessions_should_revoke_consents()
    {
        await CreateSessionAsync("sub1", "sid1", "client1");

        await _grantStore.StoreAsync(new PersistedGrant { Key = "consent1", Type = IdentityServerConstants.PersistedGrantTypes.UserConsent, SubjectId = "sub1", ClientId = "client1" });

        await _subject.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "sub1",
            RevokeTokens = false,
            SendBackchannelLogoutNotification = false
        });

        (await _grantStore.GetAsync("consent1")).Should().BeNull();
    }

    [Fact]
    public async Task RemoveSessions_should_keep_sessions_when_disabled()
    {
        var key = await CreateSessionAsync("sub1", "sid1");

        await _subject.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "sub1",
            RemoveServerSideSession = false,
            SendBackchannelLogoutNotification = false
        });

        (await _sessionStore.GetSessionAsync(key)).Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveSessions_with_empty_client_ids_should_not_revoke_any_tokens()
    {
        await CreateSessionAsync("sub1", "sid1", "client1");

        await _grantStore.StoreAsync(new PersistedGrant { Key = "rt1", Type = "refresh_token", SubjectId = "sub1", SessionId = "sid1", ClientId = "client1" });

        await _subject.RemoveSessionsAsync(new RemoveSessionsContext
        {
            SubjectId = "sub1",
            ClientIds = Array.Empty<string>(),
            SendBackchannelLogoutNotification = false
        });

        (await _grantStore.GetAsync("rt1")).Should().NotBeNull();
    }

    [Fact]
    public async Task QuerySessions_should_return_sessions()
    {
        await CreateSessionAsync("sub1", "sid1");
        await CreateSessionAsync("sub2", "sid2");

        var result = await _subject.QuerySessionsAsync(new SessionQuery { SubjectId = "sub1" });

        result.Results.Count.Should().Be(1);
        result.Results.Single().SessionId.Should().Be("sid1");
    }
}

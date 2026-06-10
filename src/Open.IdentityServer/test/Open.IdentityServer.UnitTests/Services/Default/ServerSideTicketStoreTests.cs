// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Configuration;
using Open.IdentityServer.Extensions;
using Open.IdentityServer.Models;
using Open.IdentityServer.Services;
using Open.IdentityServer.Stores;
using Xunit;
using AwesomeAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Time.Testing;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityServer.UnitTests.Common;
using Open.IdentityServer;

namespace IdentityServer.UnitTests.Services.Default;

public class ServerSideTicketStoreTests
{
    private readonly FakeTimeProvider _clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly IdentityServerOptions _options = new IdentityServerOptions();
    private readonly InMemoryServerSideSessionStore _sessionStore;
    private readonly ServerSideTicketStore _subject;

    public ServerSideTicketStoreTests()
    {
        _sessionStore = new InMemoryServerSideSessionStore(_clock);
        _subject = new ServerSideTicketStore(
            _options,
            _sessionStore,
            new EphemeralDataProtectionProvider(),
            _clock,
            TestLogger.Create<ServerSideTicketStore>());
    }

    private AuthenticationTicket CreateTicket(string sub = "sub1", string sid = "sid1", DateTimeOffset? expires = null, string displayNameClaimType = null, string displayName = null)
    {
        var claims = new System.Collections.Generic.List<Claim> { new Claim(JwtClaimTypes.Subject, sub) };
        if (displayNameClaimType != null && displayName != null)
        {
            claims.Add(new Claim(displayNameClaimType, displayName));
        }

        var identity = new ClaimsIdentity(claims, "pwd");
        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties
        {
            ExpiresUtc = expires
        };
        properties.SetSessionId(sid);

        return new AuthenticationTicket(principal, properties, "idsrv");
    }

    [Fact]
    public async Task Store_should_create_session_record_with_metadata()
    {
        var ticket = CreateTicket(sub: "sub1", sid: "sid1", expires: _clock.GetUtcNow().AddHours(1));

        var key = await _subject.StoreAsync(ticket);

        key.Should().NotBeNullOrWhiteSpace();

        var session = await _sessionStore.GetSessionAsync(key);
        session.Should().NotBeNull();
        session.SubjectId.Should().Be("sub1");
        session.SessionId.Should().Be("sid1");
        session.Scheme.Should().Be("idsrv");
        session.Created.Should().Be(_clock.GetUtcNow().UtcDateTime);
        session.Renewed.Should().Be(session.Created);
        session.Expires.Should().Be(_clock.GetUtcNow().AddHours(1).UtcDateTime);
        session.Ticket.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Store_should_protect_ticket_at_rest()
    {
        var ticket = CreateTicket(sub: "sub1");

        var key = await _subject.StoreAsync(ticket);

        var session = await _sessionStore.GetSessionAsync(key);

        // the serialized payload must not contain recognizable plaintext claim values
        session.Ticket.Should().NotContain("sub1");
        session.Ticket.Should().NotContain("idsrv");
    }

    [Fact]
    public async Task Store_should_generate_unique_keys()
    {
        var key1 = await _subject.StoreAsync(CreateTicket());
        var key2 = await _subject.StoreAsync(CreateTicket());

        key1.Should().NotBe(key2);
    }

    [Fact]
    public async Task Store_without_sub_claim_should_fail()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("name", "alice") }, "pwd"));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties(), "idsrv");

        Func<Task> act = () => _subject.StoreAsync(ticket);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Store_should_capture_display_name_when_configured()
    {
        _options.ServerSideSessions.UserDisplayNameClaimType = "name";

        var key = await _subject.StoreAsync(CreateTicket(displayNameClaimType: "name", displayName: "Alice Smith"));

        var session = await _sessionStore.GetSessionAsync(key);
        session.DisplayName.Should().Be("Alice Smith");
    }

    [Fact]
    public async Task Retrieve_should_roundtrip_ticket()
    {
        var ticket = CreateTicket(sub: "sub1", sid: "sid1", expires: _clock.GetUtcNow().AddHours(1));
        var key = await _subject.StoreAsync(ticket);

        var result = await _subject.RetrieveAsync(key);

        result.Should().NotBeNull();
        result.Principal.GetSubjectId().Should().Be("sub1");
        result.Properties.GetSessionId().Should().Be("sid1");
        result.AuthenticationScheme.Should().Be("idsrv");
    }

    [Fact]
    public async Task Retrieve_for_unknown_key_should_return_null()
    {
        (await _subject.RetrieveAsync("unknown")).Should().BeNull();
    }

    [Fact]
    public async Task Retrieve_for_expired_session_should_return_null_and_remove_session()
    {
        var key = await _subject.StoreAsync(CreateTicket(expires: _clock.GetUtcNow().AddMinutes(5)));

        _clock.Advance(TimeSpan.FromMinutes(10));

        (await _subject.RetrieveAsync(key)).Should().BeNull();
        (await _sessionStore.GetSessionAsync(key)).Should().BeNull();
    }

    [Fact]
    public async Task Retrieve_with_tampered_ticket_should_return_null_and_remove_session()
    {
        var key = await _subject.StoreAsync(CreateTicket());

        var session = await _sessionStore.GetSessionAsync(key);
        session.Ticket = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
        await _sessionStore.UpdateSessionAsync(session);

        (await _subject.RetrieveAsync(key)).Should().BeNull();
        (await _sessionStore.GetSessionAsync(key)).Should().BeNull();
    }

    [Fact]
    public async Task Renew_should_update_session_and_preserve_created()
    {
        var key = await _subject.StoreAsync(CreateTicket(expires: _clock.GetUtcNow().AddHours(1)));
        var created = (await _sessionStore.GetSessionAsync(key)).Created;

        _clock.Advance(TimeSpan.FromMinutes(30));

        await _subject.RenewAsync(key, CreateTicket(expires: _clock.GetUtcNow().AddHours(1)));

        var session = await _sessionStore.GetSessionAsync(key);
        session.Created.Should().Be(created);
        session.Renewed.Should().Be(_clock.GetUtcNow().UtcDateTime);
        session.Expires.Should().Be(_clock.GetUtcNow().AddHours(1).UtcDateTime);
    }

    [Fact]
    public async Task Renew_for_missing_session_should_recreate_session()
    {
        await _subject.RenewAsync("key1", CreateTicket(sub: "sub1"));

        var session = await _sessionStore.GetSessionAsync("key1");
        session.Should().NotBeNull();
        session.SubjectId.Should().Be("sub1");
    }

    [Fact]
    public async Task Remove_should_delete_session()
    {
        var key = await _subject.StoreAsync(CreateTicket());

        await _subject.RemoveAsync(key);

        (await _sessionStore.GetSessionAsync(key)).Should().BeNull();
        (await _subject.RetrieveAsync(key)).Should().BeNull();
    }

    [Fact]
    public async Task GetSessions_should_return_user_sessions_with_client_ids()
    {
        var ticket = CreateTicket(sub: "sub1", sid: "sid1");
        ticket.Properties.AddClientId("client1");
        ticket.Properties.AddClientId("client2");
        await _subject.StoreAsync(ticket);

        var sessions = await _subject.GetSessionsAsync(new SessionFilter { SubjectId = "sub1" });

        sessions.Count.Should().Be(1);
        var session = sessions.Single();
        session.SubjectId.Should().Be("sub1");
        session.SessionId.Should().Be("sid1");
        session.ClientIds.Should().BeEquivalentTo(new[] { "client1", "client2" });
        session.Ticket.Should().NotBeNull();
    }

    [Fact]
    public async Task QuerySessions_should_return_user_sessions()
    {
        await _subject.StoreAsync(CreateTicket(sub: "sub1"));
        await _subject.StoreAsync(CreateTicket(sub: "sub2"));

        var result = await _subject.QuerySessionsAsync(new SessionQuery());

        result.TotalCount.Should().Be(2);
        result.Results.Select(x => x.SubjectId).Should().BeEquivalentTo(new[] { "sub1", "sub2" });
    }

    [Fact]
    public async Task GetAndRemoveExpiredSessions_should_return_removed_user_sessions()
    {
        var ticket = CreateTicket(sub: "sub1", expires: _clock.GetUtcNow().AddMinutes(5));
        ticket.Properties.AddClientId("client1");
        await _subject.StoreAsync(ticket);

        _clock.Advance(TimeSpan.FromMinutes(10));

        var removed = await _subject.GetAndRemoveExpiredSessionsAsync(10);

        removed.Count.Should().Be(1);
        removed.Single().SubjectId.Should().Be("sub1");
        removed.Single().ClientIds.Should().BeEquivalentTo(new[] { "client1" });
    }
}

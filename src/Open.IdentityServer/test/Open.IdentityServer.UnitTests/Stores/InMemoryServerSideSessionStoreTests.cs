// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Models;
using Open.IdentityServer.Stores;
using Xunit;
using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer.UnitTests.Stores;

public class InMemoryServerSideSessionStoreTests
{
    private readonly FakeTimeProvider _clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly InMemoryServerSideSessionStore _subject;

    public InMemoryServerSideSessionStoreTests()
    {
        _subject = new InMemoryServerSideSessionStore(_clock);
    }

    private ServerSideSession CreateTestObject(string key = null, string sub = null, string sid = null, string displayName = null, DateTime? expires = null)
    {
        return new ServerSideSession
        {
            Key = key ?? Guid.NewGuid().ToString(),
            Scheme = "idsrv",
            SubjectId = sub ?? Guid.NewGuid().ToString(),
            SessionId = sid ?? Guid.NewGuid().ToString(),
            DisplayName = displayName,
            Created = _clock.GetUtcNow().UtcDateTime,
            Renewed = _clock.GetUtcNow().UtcDateTime,
            Expires = expires,
            Ticket = "ticket"
        };
    }

    [Fact]
    public async Task CreateSession_should_persist_session()
    {
        var session = CreateTestObject(key: "key1");

        (await _subject.GetSessionAsync("key1")).Should().BeNull();

        await _subject.CreateSessionAsync(session);

        var item = await _subject.GetSessionAsync("key1");
        item.Should().NotBeNull();
        item.SubjectId.Should().Be(session.SubjectId);
    }

    [Fact]
    public async Task CreateSession_with_duplicate_key_should_fail()
    {
        await _subject.CreateSessionAsync(CreateTestObject(key: "key1"));

        Func<Task> act = () => _subject.CreateSessionAsync(CreateTestObject(key: "key1"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateSession_should_update_session()
    {
        await _subject.CreateSessionAsync(CreateTestObject(key: "key1", sub: "sub1"));

        var updated = CreateTestObject(key: "key1", sub: "sub2");
        await _subject.UpdateSessionAsync(updated);

        var item = await _subject.GetSessionAsync("key1");
        item.SubjectId.Should().Be("sub2");
    }

    [Fact]
    public async Task DeleteSession_should_remove_session()
    {
        await _subject.CreateSessionAsync(CreateTestObject(key: "key1"));

        await _subject.DeleteSessionAsync("key1");

        (await _subject.GetSessionAsync("key1")).Should().BeNull();
    }

    [Fact]
    public async Task DeleteSession_for_unknown_key_should_not_fail()
    {
        await _subject.DeleteSessionAsync("unknown");
    }

    [Fact]
    public async Task GetSessions_should_filter()
    {
        await _subject.CreateSessionAsync(CreateTestObject(sub: "sub1", sid: "sid1"));
        await _subject.CreateSessionAsync(CreateTestObject(sub: "sub1", sid: "sid2"));
        await _subject.CreateSessionAsync(CreateTestObject(sub: "sub2", sid: "sid3"));

        (await _subject.GetSessionsAsync(new SessionFilter { SubjectId = "sub1" })).Count.Should().Be(2);
        (await _subject.GetSessionsAsync(new SessionFilter { SessionId = "sid3" })).Count.Should().Be(1);
        (await _subject.GetSessionsAsync(new SessionFilter { SubjectId = "sub1", SessionId = "sid2" })).Count.Should().Be(1);
        (await _subject.GetSessionsAsync(new SessionFilter { SubjectId = "sub3" })).Count.Should().Be(0);
    }

    [Fact]
    public async Task GetSessions_without_filter_values_should_fail()
    {
        Func<Task> act = () => _subject.GetSessionsAsync(new SessionFilter());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteSessions_should_filter()
    {
        await _subject.CreateSessionAsync(CreateTestObject(key: "key1", sub: "sub1", sid: "sid1"));
        await _subject.CreateSessionAsync(CreateTestObject(key: "key2", sub: "sub1", sid: "sid2"));
        await _subject.CreateSessionAsync(CreateTestObject(key: "key3", sub: "sub2", sid: "sid3"));

        await _subject.DeleteSessionsAsync(new SessionFilter { SubjectId = "sub1" });

        (await _subject.GetSessionAsync("key1")).Should().BeNull();
        (await _subject.GetSessionAsync("key2")).Should().BeNull();
        (await _subject.GetSessionAsync("key3")).Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteSessions_without_filter_values_should_fail()
    {
        Func<Task> act = () => _subject.DeleteSessionsAsync(new SessionFilter());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAndRemoveExpiredSessions_should_only_remove_expired_sessions()
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        await _subject.CreateSessionAsync(CreateTestObject(key: "expired1", expires: now.AddMinutes(-5)));
        await _subject.CreateSessionAsync(CreateTestObject(key: "expired2", expires: now.AddMinutes(-1)));
        await _subject.CreateSessionAsync(CreateTestObject(key: "valid", expires: now.AddMinutes(5)));
        await _subject.CreateSessionAsync(CreateTestObject(key: "no_expiry", expires: null));

        var removed = await _subject.GetAndRemoveExpiredSessionsAsync(10);

        removed.Select(x => x.Key).Should().BeEquivalentTo(new[] { "expired1", "expired2" });
        (await _subject.GetSessionAsync("expired1")).Should().BeNull();
        (await _subject.GetSessionAsync("expired2")).Should().BeNull();
        (await _subject.GetSessionAsync("valid")).Should().NotBeNull();
        (await _subject.GetSessionAsync("no_expiry")).Should().NotBeNull();
    }

    [Fact]
    public async Task GetAndRemoveExpiredSessions_should_honor_count()
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        await _subject.CreateSessionAsync(CreateTestObject(expires: now.AddMinutes(-5)));
        await _subject.CreateSessionAsync(CreateTestObject(expires: now.AddMinutes(-5)));
        await _subject.CreateSessionAsync(CreateTestObject(expires: now.AddMinutes(-5)));

        var removed = await _subject.GetAndRemoveExpiredSessionsAsync(2);

        removed.Count.Should().Be(2);
        (await _subject.GetAndRemoveExpiredSessionsAsync(2)).Count.Should().Be(1);
    }

    [Fact]
    public async Task QuerySessions_should_filter()
    {
        await _subject.CreateSessionAsync(CreateTestObject(sub: "sub1", sid: "sid1", displayName: "Alice Smith"));
        await _subject.CreateSessionAsync(CreateTestObject(sub: "sub1", sid: "sid2", displayName: "Alice Smith"));
        await _subject.CreateSessionAsync(CreateTestObject(sub: "sub2", sid: "sid3", displayName: "Bob Jones"));

        (await _subject.QuerySessionsAsync(new SessionQuery { SubjectId = "sub1" })).Results.Count.Should().Be(2);
        (await _subject.QuerySessionsAsync(new SessionQuery { SessionId = "sid3" })).Results.Count.Should().Be(1);
        (await _subject.QuerySessionsAsync(new SessionQuery { DisplayName = "alice" })).Results.Count.Should().Be(2);
        (await _subject.QuerySessionsAsync(new SessionQuery())).Results.Count.Should().Be(3);
    }

    [Fact]
    public async Task QuerySessions_should_page_forward_and_backward()
    {
        for (var i = 0; i < 25; i++)
        {
            await _subject.CreateSessionAsync(CreateTestObject(sub: "sub" + i));
        }

        var page1 = await _subject.QuerySessionsAsync(new SessionQuery { CountRequested = 10 });
        page1.Results.Count.Should().Be(10);
        page1.TotalCount.Should().Be(25);
        page1.TotalPages.Should().Be(3);
        page1.HasPrevResults.Should().BeFalse();
        page1.HasNextResults.Should().BeTrue();

        var page2 = await _subject.QuerySessionsAsync(new SessionQuery { CountRequested = 10, ResultsToken = page1.ResultsToken });
        page2.Results.Count.Should().Be(10);
        page2.HasPrevResults.Should().BeTrue();
        page2.HasNextResults.Should().BeTrue();
        page2.Results.Select(x => x.Key).Should().NotIntersectWith(page1.Results.Select(x => x.Key));

        var page3 = await _subject.QuerySessionsAsync(new SessionQuery { CountRequested = 10, ResultsToken = page2.ResultsToken });
        page3.Results.Count.Should().Be(5);
        page3.HasPrevResults.Should().BeTrue();
        page3.HasNextResults.Should().BeFalse();

        var page2Again = await _subject.QuerySessionsAsync(new SessionQuery { CountRequested = 10, ResultsToken = page3.ResultsToken, RequestPriorResults = true });
        page2Again.Results.Select(x => x.Key).Should().BeEquivalentTo(page2.Results.Select(x => x.Key));
    }

    [Fact]
    public async Task QuerySessions_with_malformed_token_should_return_first_page()
    {
        await _subject.CreateSessionAsync(CreateTestObject());
        await _subject.CreateSessionAsync(CreateTestObject());

        var result = await _subject.QuerySessionsAsync(new SessionQuery { ResultsToken = "not-a-valid-token" });

        result.Results.Count.Should().Be(2);
        result.HasPrevResults.Should().BeFalse();
    }
}

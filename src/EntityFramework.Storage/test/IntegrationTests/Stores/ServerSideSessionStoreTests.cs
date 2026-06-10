// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Mappers;
using Open.IdentityServer.EntityFramework.Options;
using Open.IdentityServer.EntityFramework.Stores;
using Open.IdentityServer.Models;
using Open.IdentityServer.Stores;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Open.IdentityServer.EntityFramework.IntegrationTests.Stores;

public class ServerSideSessionStoreTests : IntegrationTest<ServerSideSessionStoreTests, PersistedGrantDbContext, OperationalStoreOptions>
{
    public ServerSideSessionStoreTests(DatabaseProviderFixture<PersistedGrantDbContext> fixture) : base(fixture)
    {
        foreach (var row in TestDatabaseProviders)
        {
            using var context = new PersistedGrantDbContext(row.Data, StoreOptions);
            context.Database.EnsureCreated();
        }
    }

    private static ServerSideSession CreateTestObject(string key = null, string sub = null, string sid = null, string displayName = null, DateTime? expires = null)
    {
        return new ServerSideSession
        {
            Key = key ?? Guid.NewGuid().ToString(),
            Scheme = "idsrv",
            SubjectId = sub ?? Guid.NewGuid().ToString(),
            SessionId = sid ?? Guid.NewGuid().ToString(),
            DisplayName = displayName,
            Created = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            Renewed = new DateTime(2026, 01, 01, 0, 0, 0, DateTimeKind.Utc),
            Expires = expires,
            Ticket = Guid.NewGuid().ToString()
        };
    }

    private static ServerSideSessionStore CreateStore(PersistedGrantDbContext context)
    {
        return new ServerSideSessionStore(context, TimeProvider.System, FakeLogger<ServerSideSessionStore>.Create());
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task CreateSessionAsync_WhenSessionCreated_ExpectSuccess(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateTestObject();

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var store = CreateStore(context);
            await store.CreateSessionAsync(session);
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var foundSession = context.ServerSideSessions.FirstOrDefault(x => x.Key == session.Key);
            foundSession.Should().NotBeNull();
            foundSession.SubjectId.Should().Be(session.SubjectId);
            foundSession.Data.Should().Be(session.Ticket);
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetSessionAsync_WhenSessionExists_ExpectSessionReturned(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateTestObject();

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            context.ServerSideSessions.Add(session.ToEntity());
            await context.SaveChangesAsync();
        }

        ServerSideSession foundSession;
        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var store = CreateStore(context);
            foundSession = await store.GetSessionAsync(session.Key);
        }

        foundSession.Should().NotBeNull();
        foundSession.Key.Should().Be(session.Key);
        foundSession.SubjectId.Should().Be(session.SubjectId);
        foundSession.SessionId.Should().Be(session.SessionId);
        foundSession.Ticket.Should().Be(session.Ticket);
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetSessionAsync_WhenSessionDoesNotExist_ExpectNull(DbContextOptions<PersistedGrantDbContext> options)
    {
        await using var context = new PersistedGrantDbContext(options, StoreOptions);
        var store = CreateStore(context);

        (await store.GetSessionAsync(Guid.NewGuid().ToString())).Should().BeNull();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task UpdateSessionAsync_WhenSessionExists_ExpectUpdated(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateTestObject();

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            context.ServerSideSessions.Add(session.ToEntity());
            await context.SaveChangesAsync();
        }

        session.Ticket = "updated_ticket";
        session.Renewed = new DateTime(2026, 02, 01, 0, 0, 0, DateTimeKind.Utc);

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var store = CreateStore(context);
            await store.UpdateSessionAsync(session);
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var foundSession = context.ServerSideSessions.FirstOrDefault(x => x.Key == session.Key);
            foundSession.Data.Should().Be("updated_ticket");
            foundSession.Renewed.Should().Be(session.Renewed);
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task DeleteSessionAsync_WhenSessionExists_ExpectDeleted(DbContextOptions<PersistedGrantDbContext> options)
    {
        var session = CreateTestObject();

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            context.ServerSideSessions.Add(session.ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var store = CreateStore(context);
            await store.DeleteSessionAsync(session.Key);
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            context.ServerSideSessions.FirstOrDefault(x => x.Key == session.Key).Should().BeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetSessionsAsync_ShouldFilter(DbContextOptions<PersistedGrantDbContext> options)
    {
        var sub = Guid.NewGuid().ToString();
        var sid = Guid.NewGuid().ToString();

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            context.ServerSideSessions.Add(CreateTestObject(sub: sub, sid: sid).ToEntity());
            context.ServerSideSessions.Add(CreateTestObject(sub: sub).ToEntity());
            context.ServerSideSessions.Add(CreateTestObject().ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var store = CreateStore(context);

            (await store.GetSessionsAsync(new SessionFilter { SubjectId = sub })).Count.Should().Be(2);
            (await store.GetSessionsAsync(new SessionFilter { SessionId = sid })).Count.Should().Be(1);
            (await store.GetSessionsAsync(new SessionFilter { SubjectId = sub, SessionId = sid })).Count.Should().Be(1);
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetSessionsAsync_WithoutFilterValues_ExpectException(DbContextOptions<PersistedGrantDbContext> options)
    {
        await using var context = new PersistedGrantDbContext(options, StoreOptions);
        var store = CreateStore(context);

        Func<Task> act = () => store.GetSessionsAsync(new SessionFilter());

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task DeleteSessionsAsync_ShouldFilter(DbContextOptions<PersistedGrantDbContext> options)
    {
        var sub = Guid.NewGuid().ToString();
        var otherKey = Guid.NewGuid().ToString();

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            context.ServerSideSessions.Add(CreateTestObject(sub: sub).ToEntity());
            context.ServerSideSessions.Add(CreateTestObject(sub: sub).ToEntity());
            context.ServerSideSessions.Add(CreateTestObject(key: otherKey).ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var store = CreateStore(context);
            await store.DeleteSessionsAsync(new SessionFilter { SubjectId = sub });
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            context.ServerSideSessions.Count(x => x.SubjectId == sub).Should().Be(0);
            context.ServerSideSessions.FirstOrDefault(x => x.Key == otherKey).Should().NotBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task GetAndRemoveExpiredSessionsAsync_ShouldOnlyRemoveExpiredSessions(DbContextOptions<PersistedGrantDbContext> options)
    {
        var expiredKey = Guid.NewGuid().ToString();
        var validKey = Guid.NewGuid().ToString();
        var noExpiryKey = Guid.NewGuid().ToString();

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            context.ServerSideSessions.Add(CreateTestObject(key: expiredKey, expires: DateTime.UtcNow.AddMinutes(-5)).ToEntity());
            context.ServerSideSessions.Add(CreateTestObject(key: validKey, expires: DateTime.UtcNow.AddMinutes(30)).ToEntity());
            context.ServerSideSessions.Add(CreateTestObject(key: noExpiryKey).ToEntity());
            await context.SaveChangesAsync();
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var store = CreateStore(context);
            var removed = await store.GetAndRemoveExpiredSessionsAsync(10);

            removed.Select(x => x.Key).Should().Contain(expiredKey);
            removed.Select(x => x.Key).Should().NotContain(new[] { validKey, noExpiryKey });
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            context.ServerSideSessions.FirstOrDefault(x => x.Key == expiredKey).Should().BeNull();
            context.ServerSideSessions.FirstOrDefault(x => x.Key == validKey).Should().NotBeNull();
            context.ServerSideSessions.FirstOrDefault(x => x.Key == noExpiryKey).Should().NotBeNull();
        }
    }

    [Theory, MemberData(nameof(TestDatabaseProviders))]
    public async Task QuerySessionsAsync_ShouldFilterAndPage(DbContextOptions<PersistedGrantDbContext> options)
    {
        var sub = Guid.NewGuid().ToString();
        var displayName = Guid.NewGuid().ToString();

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            for (var i = 0; i < 12; i++)
            {
                context.ServerSideSessions.Add(CreateTestObject(sub: sub, displayName: displayName).ToEntity());
            }
            await context.SaveChangesAsync();
        }

        await using (var context = new PersistedGrantDbContext(options, StoreOptions))
        {
            var store = CreateStore(context);

            var page1 = await store.QuerySessionsAsync(new SessionQuery { SubjectId = sub, CountRequested = 5 });
            page1.Results.Count.Should().Be(5);
            page1.TotalCount.Should().Be(12);
            page1.TotalPages.Should().Be(3);
            page1.HasPrevResults.Should().BeFalse();
            page1.HasNextResults.Should().BeTrue();

            var page2 = await store.QuerySessionsAsync(new SessionQuery { SubjectId = sub, CountRequested = 5, ResultsToken = page1.ResultsToken });
            page2.Results.Count.Should().Be(5);
            page2.HasPrevResults.Should().BeTrue();
            page2.HasNextResults.Should().BeTrue();
            page2.Results.Select(x => x.Key).Should().NotIntersectWith(page1.Results.Select(x => x.Key));

            var page3 = await store.QuerySessionsAsync(new SessionQuery { SubjectId = sub, CountRequested = 5, ResultsToken = page2.ResultsToken });
            page3.Results.Count.Should().Be(2);
            page3.HasPrevResults.Should().BeTrue();
            page3.HasNextResults.Should().BeFalse();

            var page2Again = await store.QuerySessionsAsync(new SessionQuery { SubjectId = sub, CountRequested = 5, ResultsToken = page3.ResultsToken, RequestPriorResults = true });
            page2Again.Results.Select(x => x.Key).Should().BeEquivalentTo(page2.Results.Select(x => x.Key));

            var byDisplayName = await store.QuerySessionsAsync(new SessionQuery { DisplayName = displayName });
            byDisplayName.Results.Count.Should().Be(12);
        }
    }
}

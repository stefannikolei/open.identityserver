// Copyright (c) 2026, Rock Solid Knowledge Ltd
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Open.IdentityServer.Extensions;
using Open.IdentityServer.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Open.IdentityServer.Stores;

/// <summary>
/// In-memory implementation of <see cref="IServerSideSessionStore"/>.
/// Intended for development and testing; state is lost when the process exits.
/// </summary>
public class InMemoryServerSideSessionStore : IServerSideSessionStore
{
    private readonly ConcurrentDictionary<string, Entry> _store = new ConcurrentDictionary<string, Entry>();
    private readonly TimeProvider _clock;
    private long _sequence;

    private class Entry
    {
        public long Id;
        public ServerSideSession Session;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryServerSideSessionStore"/> class.
    /// </summary>
    /// <param name="clock">The clock. If <c>null</c>, the system clock is used.</param>
    public InMemoryServerSideSessionStore(TimeProvider clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public Task CreateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (String.IsNullOrWhiteSpace(session.Key)) throw new ArgumentException("Key is required.", nameof(session));

        var entry = new Entry
        {
            Id = Interlocked.Increment(ref _sequence),
            Session = session
        };

        if (!_store.TryAdd(session.Key, entry))
        {
            throw new InvalidOperationException("A session with the key already exists.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ServerSideSession> GetSessionAsync(string key, CancellationToken cancellationToken = default)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        _store.TryGetValue(key, out var entry);

        return Task.FromResult(entry?.Session);
    }

    /// <inheritdoc/>
    public Task UpdateSessionAsync(ServerSideSession session, CancellationToken cancellationToken = default)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        if (String.IsNullOrWhiteSpace(session.Key)) throw new ArgumentException("Key is required.", nameof(session));

        if (_store.TryGetValue(session.Key, out var entry))
        {
            entry.Session = session;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DeleteSessionAsync(string key, CancellationToken cancellationToken = default)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));

        _store.TryRemove(key, out _);

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<ServerSideSession>> GetSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default)
    {
        filter.Validate();

        var results = Filter(filter).Select(x => x.Session).ToArray();

        return Task.FromResult<IReadOnlyCollection<ServerSideSession>>(results);
    }

    /// <inheritdoc/>
    public Task DeleteSessionsAsync(SessionFilter filter, CancellationToken cancellationToken = default)
    {
        filter.Validate();

        var keys = Filter(filter).Select(x => x.Session.Key).ToArray();

        foreach (var key in keys)
        {
            _store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyCollection<ServerSideSession>> GetAndRemoveExpiredSessionsAsync(int count, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        var expired = _store.Values
            .Where(x => x.Session.Expires != null && x.Session.Expires <= now)
            .OrderBy(x => x.Id)
            .Take(count)
            .ToArray();

        var removed = new List<ServerSideSession>(expired.Length);
        foreach (var entry in expired)
        {
            if (_store.TryRemove(entry.Session.Key, out var value))
            {
                removed.Add(value.Session);
            }
        }

        return Task.FromResult<IReadOnlyCollection<ServerSideSession>>(removed);
    }

    /// <inheritdoc/>
    public Task<QueryResult<ServerSideSession>> QuerySessionsAsync(SessionQuery filter = null, CancellationToken cancellationToken = default)
    {
        filter ??= new SessionQuery();

        var countRequested = filter.CountRequested;
        if (countRequested <= 0) countRequested = 25;

        var query = _store.Values.AsEnumerable();

        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.Session.SubjectId == filter.SubjectId);
        }
        if (!String.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.Session.SessionId == filter.SessionId);
        }
        if (!String.IsNullOrWhiteSpace(filter.DisplayName))
        {
            query = query.Where(x => x.Session.DisplayName != null &&
                                     x.Session.DisplayName.Contains(filter.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        var matching = query.OrderBy(x => x.Id).ToArray();
        var totalCount = matching.Length;
        var totalPages = (int)Math.Max(1, Math.Ceiling(totalCount / (double)countRequested));

        Entry[] page;
        if (PaginationToken.TryParse(filter.ResultsToken, out var token))
        {
            page = filter.RequestPriorResults
                ? matching.Where(x => x.Id < token.First).TakeLast(countRequested).ToArray()
                : matching.Where(x => x.Id > token.Last).Take(countRequested).ToArray();
        }
        else
        {
            page = matching.Take(countRequested).ToArray();
        }

        var result = new QueryResult<ServerSideSession>
        {
            Results = page.Select(x => x.Session).ToArray(),
            TotalCount = totalCount,
            TotalPages = totalPages
        };

        if (page.Length > 0)
        {
            var first = page[0].Id;
            var last = page[page.Length - 1].Id;

            result.ResultsToken = new PaginationToken(first, last).ToString();
            result.HasPrevResults = matching.Any(x => x.Id < first);
            result.HasNextResults = matching.Any(x => x.Id > last);
        }

        return Task.FromResult(result);
    }

    private IEnumerable<Entry> Filter(SessionFilter filter)
    {
        var query = _store.Values.AsEnumerable();

        if (!String.IsNullOrWhiteSpace(filter.SubjectId))
        {
            query = query.Where(x => x.Session.SubjectId == filter.SubjectId);
        }
        if (!String.IsNullOrWhiteSpace(filter.SessionId))
        {
            query = query.Where(x => x.Session.SessionId == filter.SessionId);
        }

        return query.ToArray();
    }
}

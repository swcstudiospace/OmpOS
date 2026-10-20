using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;

namespace OmpOS.Sessions;

/// <summary>
/// Thread-safe in-memory <see cref="ISessionRepository"/>. Each session has its own lock, so appends to
/// different sessions never contend, and appends to one session are serialized, which makes Seq assignment atomic.
/// </summary>
public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly ConcurrentDictionary<string, Entry> _sessions = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<Session?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_sessions.TryGetValue(id, out var entry))
        {
            return Task.FromResult<Session?>(null);
        }

        lock (entry.Gate)
        {
            return Task.FromResult<Session?>(entry.Header is null ? null : entry.Header with { Events = entry.Log });
        }
    }

    /// <inheritdoc />
    public Task SaveAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = _sessions.GetOrAdd(session.Id, static _ => new Entry());
        lock (entry.Gate)
        {
            entry.Header = session with { Events = ImmutableList<StreamEvent>.Empty };
            if (session.Events.Count >= entry.Log.Count)
            {
                entry.Log = session.Events.ToImmutableList();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AppendAsync(string sessionId, StreamEvent evt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(evt);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = Find(sessionId);
        lock (entry.Gate)
        {
            var seq = entry.Log.Count == 0 ? 1 : entry.Log[^1].Seq + 1;
            entry.Log = entry.Log.Add(evt with { Seq = seq });
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StreamEvent> ListFromSeqAsync(
        string sessionId,
        long fromSeq,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        // Yield once so the enumeration is genuinely asynchronous and honours cancellation from the first MoveNext.
        await Task.Yield();

        var snapshot = Snapshot(sessionId);
        foreach (var evt in snapshot)
        {
            if (evt.Seq < fromSeq)
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            yield return evt;
        }
    }

    private ImmutableList<StreamEvent> Snapshot(string sessionId)
    {
        var entry = Find(sessionId);
        lock (entry.Gate)
        {
            return entry.Log;
        }
    }

    /// <summary>Returns the entry for a saved session; an entry without a saved header does not count as existing.</summary>
    private Entry Find(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var entry))
        {
            lock (entry.Gate)
            {
                if (entry.Header is not null)
                {
                    return entry;
                }
            }
        }

        throw new KeyNotFoundException($"Session '{sessionId}' does not exist.");
    }

    /// <summary>Per-session state guarded by <see cref="Gate"/>.</summary>
    private sealed class Entry
    {
        /// <summary>The lock serializing every read and write of this session.</summary>
        public object Gate { get; } = new();

        /// <summary>The saved session header, or <see langword="null"/> until the first save completes. Its <see cref="Session.Events"/> is always empty.</summary>
        public Session? Header { get; set; }

        /// <summary>The append-only event log, ordered by Seq.</summary>
        public ImmutableList<StreamEvent> Log { get; set; } = ImmutableList<StreamEvent>.Empty;
    }
}

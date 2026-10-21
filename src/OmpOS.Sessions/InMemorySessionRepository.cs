using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Sessions;

/// <summary>
/// Thread-safe in-memory <see cref="ISessionRepository"/>. Each session has its own lock, so appends to
/// different sessions never contend, and appends to one session are serialized, which makes Seq assignment atomic.
/// Each session also has an exclusive lock for <see cref="LockSessionAsync"/>, separate from the data lock so a holder
/// of the session lock can call every other member without deadlock.
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
    /// <exception cref="InvalidOperationException">
    /// The incoming <see cref="Session.Events"/> neither extend nor agree with the stored log: an event
    /// within the common prefix carries a different <see cref="StreamEvent.Seq"/> than the stored event
    /// at the same position. The message names the session and the first conflicting position.
    /// </exception>
    public Task SaveAsync(Session session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();

        var entry = _sessions.GetOrAdd(session.Id, static _ => new Entry());
        lock (entry.Gate)
        {
            if (session.Events.Count >= entry.Log.Count)
            {
                var conflict = FirstConflict(entry.Log, session.Events);
                if (conflict >= 0)
                {
                    throw new InvalidOperationException(
                        $"Session '{session.Id}' has a conflicting event log: incoming event at index {conflict} " +
                        $"has Seq {session.Events[conflict].Seq} but the stored log has Seq {entry.Log[conflict].Seq}.");
                }
            }

            entry.Header = session with { Events = ImmutableList<StreamEvent>.Empty };
            if (session.Events.Count > entry.Log.Count)
            {
                // The incoming events extend the stored log: same Seq prefix, strictly longer.
                entry.Log = session.Events.ToImmutableList();
            }
            // Otherwise the save is stale (fewer events) or agrees exactly: keep the stored log.
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the first position where the incoming events disagree with the stored log.
    /// </summary>
    /// <param name="stored">The stored event log.</param>
    /// <param name="incoming">The incoming events, at least as long as the stored log.</param>
    /// <returns>The first conflicting index, or -1 when the stored log is a Seq-prefix of the incoming events.</returns>
    private static int FirstConflict(ImmutableList<StreamEvent> stored, IReadOnlyList<StreamEvent> incoming)
    {
        for (var i = 0; i < stored.Count; i++)
        {
            if (incoming[i].Seq != stored[i].Seq)
            {
                return i;
            }
        }

        return -1;
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

    /// <inheritdoc />
    /// <remarks>
    /// The lock needs no saved session: an entry is created on demand for the identifier. An entry without a saved header
    /// does not count as an existing session (see <see cref="GetAsync"/>).
    /// </remarks>
    public async Task<IAsyncDisposable> LockSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);

        var entry = _sessions.GetOrAdd(sessionId, static _ => new Entry());
        await entry.Exclusive.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new ExclusiveLease(entry.Exclusive);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The approval belongs to a different session.</exception>
    /// <exception cref="KeyNotFoundException">No session has the given identifier.</exception>
    public Task<bool> TryAddToolApprovalAsync(string sessionId, ToolApprovalRecord approval, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(approval);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(approval.SessionId, sessionId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Tool approval '{approval.RequestId}' belongs to session '{approval.SessionId}', not to '{sessionId}'.",
                nameof(approval));
        }

        var entry = Find(sessionId);
        lock (entry.Gate)
        {
            if (entry.Approvals.ContainsKey(approval.RequestId))
            {
                return Task.FromResult(false);
            }

            entry.Approvals.Add(approval.RequestId, approval);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<ToolApprovalRecord?> GetToolApprovalAsync(string sessionId, string requestId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(requestId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            return Task.FromResult<ToolApprovalRecord?>(null);
        }

        lock (entry.Gate)
        {
            return Task.FromResult<ToolApprovalRecord?>(entry.Approvals.GetValueOrDefault(requestId));
        }
    }

    /// <inheritdoc />
    public Task<bool> TryDecideToolApprovalAsync(
        string sessionId,
        string requestId,
        PermissionDecision decision,
        DateTimeOffset decidedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(requestId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_sessions.TryGetValue(sessionId, out var entry))
        {
            return Task.FromResult(false);
        }

        lock (entry.Gate)
        {
            if (!entry.Approvals.TryGetValue(requestId, out var record) || record.Decision is not null)
            {
                return Task.FromResult(false);
            }

            entry.Approvals[requestId] = record with { Decision = decision, DecidedAt = decidedAt };
            return Task.FromResult(true);
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

        /// <summary>The exclusive session lock taken by <see cref="LockSessionAsync"/>. Held across awaits, so it is not <see cref="Gate"/>.</summary>
        public SemaphoreSlim Exclusive { get; } = new(1, 1);

        /// <summary>The saved session header, or <see langword="null"/> until the first save completes. Its <see cref="Session.Events"/> is always empty.</summary>
        public Session? Header { get; set; }

        /// <summary>The append-only event log, ordered by Seq.</summary>
        public ImmutableList<StreamEvent> Log { get; set; } = ImmutableList<StreamEvent>.Empty;

        /// <summary>The tool approvals of this session, keyed by request identifier and guarded by <see cref="Gate"/>.</summary>
        public Dictionary<string, ToolApprovalRecord> Approvals { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>Releases one session lock exactly once, however many times it is disposed.</summary>
    private sealed class ExclusiveLease(SemaphoreSlim exclusive) : IAsyncDisposable
    {
        private int _released;

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                exclusive.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}

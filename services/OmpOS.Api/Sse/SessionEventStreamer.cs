using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OmpOS.Api.Metrics;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Sessions;

namespace OmpOS.Api.Sse;

/// <summary>
/// Per-session SSE broadcaster. The session log is the source of truth: subscribers replay
/// from the log, then tail live appends. A wake-up signal fires on every append; on wake the
/// subscriber re-queries the log from its last delivered Seq, so the replay/live handoff can
/// neither gap (the signal cannot be lost: a pre-wait signal releases the next wait) nor
/// duplicate (events at or below the last delivered Seq are skipped).
/// </summary>
internal sealed class SessionEventStreamer
{
    private readonly ConcurrentDictionary<string, SessionSignal> _signals = new(StringComparer.Ordinal);
    private readonly Lock _signalLock = new();
    private readonly ISessionRepository _repository;
    private readonly ApiMetrics _metrics;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="SessionEventStreamer"/> class.</summary>
    /// <param name="repository">The session store (source of truth).</param>
    /// <param name="metrics">The API metrics.</param>
    /// <param name="timeProvider">The clock used for latency measurement.</param>
    public SessionEventStreamer(ISessionRepository repository, ApiMetrics metrics, TimeProvider timeProvider)
    {
        _repository = repository;
        _metrics = metrics;
        _time = timeProvider;
    }

    /// <summary>Wakes the live tails of <paramref name="sessionId"/> after an append.</summary>
    /// <param name="sessionId">The session identifier.</param>
    public void Notify(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        if (_signals.TryGetValue(sessionId, out var signal))
        {
            signal.Notify();
        }
    }

    /// <summary>Gets whether a wake-up signal is currently held for <paramref name="sessionId"/>.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>True while at least one stream holds the session's signal.</returns>
    internal bool HasSignal(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return _signals.ContainsKey(sessionId);
    }

    /// <summary>
    /// Streams a session's events starting after <paramref name="afterSeq"/>, replaying the log
    /// first and then tailing live appends until a terminal event or cancellation.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="afterSeq">Events with Seq above this value are delivered (exclusive resume).</param>
    /// <param name="cancellationToken">Cancels the stream (client disconnect).</param>
    /// <returns>The event stream ending on a terminal event.</returns>
    public async IAsyncEnumerable<StreamEvent> SubscribeAsync(
        string sessionId,
        long afterSeq,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        var signal = Acquire(sessionId);

        try
        {
            var last = Math.Max(0, afterSeq);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var delivered = false;
                await foreach (var evt in ReplayAsync(sessionId, Floor(last), cancellationToken).ConfigureAwait(false))
                {
                    delivered = true;
                    last = evt.Seq;
                    yield return evt;
                    if (StreamEventKind.IsTerminal(evt.Payload))
                    {
                        yield break;
                    }
                }

                if (!delivered && await IsSettledAsync(sessionId, cancellationToken).ConfigureAwait(false))
                {
                    await foreach (var evt in ReplayAsync(sessionId, Floor(last), cancellationToken).ConfigureAwait(false))
                    {
                        delivered = true;
                        last = evt.Seq;
                        yield return evt;
                        if (StreamEventKind.IsTerminal(evt.Payload))
                        {
                            yield break;
                        }
                    }

                    if (!delivered)
                    {
                        yield break;
                    }
                }

                await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Release(sessionId, signal);
        }
    }

    private SessionSignal Acquire(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        lock (_signalLock)
        {
            var signal = _signals.GetOrAdd(sessionId, static _ => new SessionSignal());
            signal.AddRef();
            return signal;
        }
    }

    private void Release(string sessionId, SessionSignal signal)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(signal);
        lock (_signalLock)
        {
            signal.Release();
            if (signal.RefCount == 0)
            {
                _signals.TryRemove(new KeyValuePair<string, SessionSignal>(sessionId, signal));
                signal.Dispose();
            }
        }
    }

    private async IAsyncEnumerable<StreamEvent> ReplayAsync(
        string sessionId,
        long fromSeq,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var evt in _repository.ListFromSeqAsync(sessionId, fromSeq, cancellationToken).ConfigureAwait(false))
        {
            _metrics.RecordStreamLatency((_time.GetUtcNow() - evt.Timestamp).TotalMilliseconds);
            yield return evt;
        }
    }

    private async Task<bool> IsSettledAsync(string sessionId, CancellationToken cancellationToken)
    {
        var current = await _repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return current is not null
            && current.Status is SessionStatus.Completed or SessionStatus.Failed or SessionStatus.Cancelled;
    }

    private static long Floor(long last) => last == long.MaxValue ? long.MaxValue : last + 1;

    /// <summary>Coalescing wake-up: releases collapse into one permit because waiters always re-query.</summary>
    private sealed class SessionSignal : IDisposable
    {
        private readonly SemaphoreSlim _gate = new(0, 1);
        private bool _disposed;

        /// <summary>Gets the number of streams currently holding this signal. Guarded by the streamer's lock.</summary>
        public int RefCount { get; private set; }

        /// <summary>Records one more stream holding this signal.</summary>
        public void AddRef() => RefCount++;

        /// <summary>Releases one stream's hold on this signal.</summary>
        public void Release() => RefCount--;

        /// <summary>Signals waiters that the log grew.</summary>
        public void Notify()
        {
            if (_disposed)
            {
                return;
            }

            if (_gate.CurrentCount == 0)
            {
                try
                {
                    _gate.Release();
                }
                catch (SemaphoreFullException)
                {
                    // A concurrent notify won the permit; the waiter still re-queries.
                }
                catch (ObjectDisposedException)
                {
                    // Shutdown race; waiters observe cancellation instead.
                }
            }
        }

        /// <summary>Waits for the next append signal.</summary>
        /// <param name="cancellationToken">Cancels the wait.</param>
        /// <returns>A task representing the wait.</returns>
        public Task WaitAsync(CancellationToken cancellationToken) => _gate.WaitAsync(cancellationToken);

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _gate.Dispose();
            }
        }
    }
}

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using OmpOS.Api.Metrics;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Sessions;

namespace OmpOS.Api.Sse;

/// <summary>
/// Per-session SSE broadcaster. The session log is the source of truth: subscribers replay
/// from the log, then tail live appends. Every append wakes every waiting subscriber through a
/// broadcast signal; on wake a subscriber re-queries the log from its last delivered Seq. A
/// subscriber takes its wake-up before each log read, so an append that lands during the read
/// is never missed, and events at or below the last delivered Seq are skipped (no duplicates).
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
                // Take the wake-up before reading the log: an append that lands during the read completes it.
                var wake = signal.Next();
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

                await wake.WaitAsync(cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// Broadcast wake-up for one session. <see cref="Notify"/> completes the current generation, so every
    /// subscriber waiting on it wakes; waiters always re-query the log, so a wake carries no payload.
    /// </summary>
    private sealed class SessionSignal
    {
        private readonly Lock _lock = new();
        private TaskCompletionSource<bool> _generation = NewGeneration();

        /// <summary>Gets the number of streams currently holding this signal. Guarded by the streamer's lock.</summary>
        public int RefCount { get; private set; }

        /// <summary>Records one more stream holding this signal.</summary>
        public void AddRef() => RefCount++;

        /// <summary>Releases one stream's hold on this signal.</summary>
        public void Release() => RefCount--;

        /// <summary>Returns the task that completes on the next <see cref="Notify"/>. Take it before reading the log.</summary>
        /// <returns>The task for the current generation.</returns>
        public Task Next()
        {
            lock (_lock)
            {
                return _generation.Task;
            }
        }

        /// <summary>Wakes every waiter on the current generation and starts the next one.</summary>
        public void Notify()
        {
            lock (_lock)
            {
                _generation.TrySetResult(true);
                _generation = NewGeneration();
            }
        }

        private static TaskCompletionSource<bool> NewGeneration() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

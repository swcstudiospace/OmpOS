using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Autonogrammer.Api.Metrics;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.Sse;

/// <summary>
/// Per-session SSE broadcaster. The session log is the source of truth: subscribers replay
/// from the log, then tail live appends. Every append wakes every waiting subscriber through a
/// broadcast signal; on wake a subscriber re-queries the log from its last delivered Seq. A
/// subscriber takes its wake-up before each log read, so an append that lands during the read
/// is never missed, and events at or below the last delivered Seq are skipped (no duplicates).
/// A waiting subscriber also re-reads the log every <see cref="PollInterval"/> without a local wake-up. Appends made by another replica
/// reach this stream through the session signal channel (see <see cref="SessionChangePublisher"/>); the poll is the fallback that keeps the
/// stream correct when signals are lost or unavailable, so an append reaches it within one interval. The poll needs no listener connection,
/// survives pooled connections and restarts, and bounds latency; the local <see cref="Notify"/> remains the fast path for appends made in this process.
/// </summary>
internal sealed class SessionEventStreamer
{
    /// <summary>How long a waiting subscriber sleeps before it re-reads the log without a local wake-up.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

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
                // Take the wake-up before reading the log: an append that lands during the read cancels it.
                var wake = signal.NextWake();
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

                await WaitForWakeOrPollAsync(wake, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            Release(sessionId, signal);
        }
    }

    private static async Task WaitForWakeOrPollAsync(CancellationToken wake, CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(wake, cancellationToken);
        try
        {
            await Task.Delay(PollInterval, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled by Notify (a wake) or by the caller; the check below rethrows only for the caller.
        }

        cancellationToken.ThrowIfCancellationRequested();
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

    /// <summary>
    /// Broadcast wake-up for one session. <see cref="Notify"/> cancels the current generation, so every
    /// subscriber waiting on it wakes; waiters always re-query the log, so a wake carries no payload.
    /// </summary>
    private sealed class SessionSignal : IDisposable
    {
        private readonly Lock _lock = new();
        private CancellationTokenSource _generation = new();
        private bool _disposed;

        /// <summary>Gets the number of streams currently holding this signal. Guarded by the streamer's lock.</summary>
        public int RefCount { get; private set; }

        /// <summary>Records one more stream holding this signal.</summary>
        public void AddRef() => RefCount++;

        /// <summary>Releases one stream's hold on this signal.</summary>
        public void Release() => RefCount--;

        /// <summary>Returns the token that is cancelled by the next <see cref="Notify"/>. Take it before reading the log.</summary>
        /// <returns>The token for the current generation.</returns>
        public CancellationToken NextWake()
        {
            lock (_lock)
            {
                return _generation.Token;
            }
        }

        /// <summary>Wakes every waiter on the current generation and starts the next one.</summary>
        public void Notify()
        {
            CancellationTokenSource woken;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                woken = _generation;
                _generation = new CancellationTokenSource();
            }

            // Cancel outside the lock: cancellation callbacks run synchronously and must not run under it.
            woken.Cancel();
        }

        /// <summary>Disposes the current generation. Called once no stream holds the signal.</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _generation.Dispose();
            }
        }
    }
}

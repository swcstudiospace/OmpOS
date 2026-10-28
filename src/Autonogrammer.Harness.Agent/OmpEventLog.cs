using System.Runtime.CompilerServices;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Agent;

/// <summary>
/// The append-only, sequence-numbered event log of one agent session. Sequences start at 1 and grow by one per appended
/// event. The log records the last acknowledged sequence, which never moves backwards. Appends and reads may run on
/// different tasks.
/// </summary>
public sealed class OmpEventLog
{
    private readonly System.Threading.Lock _gate = new();
    private readonly List<OmpEvent> _events = [];
    private TaskCompletionSource _appended = NewSignal();
    private long _acknowledged;
    private bool _completed;

    /// <summary>Initializes a new instance of the <see cref="OmpEventLog"/> class for one agent session.</summary>
    /// <param name="sessionId">The session the log records.</param>
    public OmpEventLog(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        SessionId = sessionId;
    }

    /// <summary>Gets the session the log records.</summary>
    public string SessionId { get; }

    /// <summary>Gets the sequence of the last appended event, or 0 when the log is empty.</summary>
    public long LastSequence
    {
        get
        {
            lock (_gate)
            {
                return _events.Count;
            }
        }
    }

    /// <summary>Gets the last acknowledged sequence, or 0 when nothing is acknowledged.</summary>
    public long AcknowledgedSequence
    {
        get
        {
            lock (_gate)
            {
                return _acknowledged;
            }
        }
    }

    /// <summary>Appends one event. Its sequence is the next one in the session.</summary>
    /// <param name="payload">The event content.</param>
    /// <param name="correlationId">The request the event belongs to.</param>
    /// <param name="timestamp">When the event was recorded.</param>
    /// <returns>The appended envelope.</returns>
    /// <exception cref="InvalidOperationException">The log is complete.</exception>
    public OmpEvent Append(HarnessEvent payload, string correlationId, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        lock (_gate)
        {
            if (_completed)
            {
                throw new InvalidOperationException("The event log is complete; no event can be appended.");
            }

            OmpEvent appended = new(IOmpAgentAdapter.Version, SessionId, _events.Count + 1L, payload, timestamp, correlationId);
            _events.Add(appended);
            Signal();
            return appended;
        }
    }

    /// <summary>Returns the events whose sequence is greater than <paramref name="afterSequence"/>, in sequence order.</summary>
    /// <param name="afterSequence">The last sequence the reader holds. 0 returns every event.</param>
    /// <returns>A snapshot of the matching events, empty when none follow.</returns>
    public IReadOnlyList<OmpEvent> ReadAfter(long afterSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        lock (_gate)
        {
            return Slice(afterSequence);
        }
    }

    /// <summary>
    /// Records that every event up to <paramref name="sequence"/> is handled. A value below the recorded acknowledgement
    /// is ignored, so the acknowledgement never moves backwards.
    /// </summary>
    /// <param name="sequence">The last handled sequence.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="sequence"/> is negative or names an event that has not been appended.
    /// </exception>
    public void Acknowledge(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        lock (_gate)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(sequence, (long)_events.Count);
            if (sequence > _acknowledged)
            {
                _acknowledged = sequence;
            }
        }
    }

    /// <summary>
    /// Reads the events whose sequence is greater than <paramref name="afterSequence"/>, in sequence order, and waits for
    /// events appended later. The read ends once the log is complete and every event is read.
    /// </summary>
    /// <param name="afterSequence">The last sequence the reader holds. 0 reads from the first event.</param>
    /// <param name="cancellationToken">Ends the read with an <see cref="OperationCanceledException"/>.</param>
    /// <returns>The events in sequence order.</returns>
    /// <remarks>
    /// The argument check runs on the first MoveNext of the returned enumerator, not when this method is called.
    /// Cancellation is honoured while the read waits for an event and before each event is yielded.
    /// </remarks>
    public async IAsyncEnumerable<OmpEvent> FollowAsync(long afterSequence, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterSequence);
        long cursor = afterSequence;
        while (true)
        {
            List<OmpEvent> batch;
            Task appended;
            bool completed;
            lock (_gate)
            {
                batch = Slice(cursor);
                appended = _appended.Task;
                completed = _completed;
            }

            if (batch.Count == 0)
            {
                if (completed)
                {
                    yield break;
                }

                await appended.WaitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            foreach (OmpEvent next in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                cursor = next.Sequence;
                yield return next;
            }
        }
    }

    /// <summary>
    /// Ends the log. No event can be appended afterwards, and a <see cref="FollowAsync"/> read ends once it has drained
    /// the events.
    /// </summary>
    public void Complete()
    {
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            Signal();
        }
    }

    /// <remarks>The caller holds the gate.</remarks>
    private List<OmpEvent> Slice(long afterSequence)
    {
        if (afterSequence >= _events.Count)
        {
            return [];
        }

        int start = (int)afterSequence;
        return _events.GetRange(start, _events.Count - start);
    }

    /// <remarks>The caller holds the gate.</remarks>
    private void Signal()
    {
        TaskCompletionSource previous = _appended;
        _appended = NewSignal();
        _ = previous.TrySetResult();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

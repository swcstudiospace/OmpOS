using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Agent.Tests;

/// <summary>
/// A test adapter over a real omp session driven by a recorded transcript. The session's own parser and state machine run
/// every turn. Each harness event the session yields is recorded in an <see cref="OmpEventLog"/> with the next sequence.
/// Turns are serialised: a send waits for the previous turn to settle, and concurrent sends queue on a gate.
/// A send the session refuses (for example one made before the session is Ready) throws to its caller. A turn whose stream
/// fails ends with a terminal <see cref="ErrorEvent"/>, the exception is kept in <see cref="PumpFault"/>, the log completes,
/// and the adapter accepts no further turn. Disposal cancels the running turn and waits a bounded time for it to settle.
/// </summary>
internal sealed class FakeOmpAgentAdapter : IOmpAgentAdapter
{
    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(10);

    private readonly IOmpSession _session;
    private readonly TimeProvider _clock;
    private readonly OmpEventLog _log;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly CancellationTokenSource _closing = new();
    private Task _turn = Task.CompletedTask;
    private bool _faulted;
    private Exception? _pumpFault;
    private volatile bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="FakeOmpAgentAdapter"/> class.</summary>
    /// <param name="session">The real omp session, over a scripted transport. The adapter owns and disposes it.</param>
    /// <param name="sessionId">The session identifier written into every event.</param>
    /// <param name="clock">The clock that stamps every event.</param>
    public FakeOmpAgentAdapter(IOmpSession session, string sessionId, TimeProvider clock)
    {
        _session = session;
        _clock = clock;
        _log = new OmpEventLog(sessionId);
    }

    /// <summary>Gets the session under test, for state assertions.</summary>
    public IOmpSession Session => _session;

    /// <summary>
    /// Gets the exception that ended the last failed turn, or <see langword="null"/> when no turn has failed or the turn was
    /// cancelled by disposal (a cancelled turn carries no exception). Set before the log completes, so a reader that reaches
    /// the end of the log sees it.
    /// </summary>
    public Exception? PumpFault => _pumpFault;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => _session.CreateAsync(cancellationToken);

    /// <inheritdoc />
    public async Task SendAsync(string prompt, PromptOptions options, string correlationId, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using CancellationTokenSource gateWait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _closing.Token);
        await _sendGate.WaitAsync(gateWait.Token).ConfigureAwait(false);
        try
        {
            // SettleTurn never throws, so the previous turn's task completes normally even when its stream failed.
            await _turn.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (_faulted)
            {
                throw new InvalidOperationException("The session faulted and its event log is complete; no further turn is accepted.");
            }

            IAsyncEnumerator<HarnessEvent> stream = _session.SubmitPromptAsync(prompt, options, _closing.Token)
                .GetAsyncEnumerator(_closing.Token);
            bool admitted = false;
            bool more;
            try
            {
                // The first MoveNext admits the turn, so a refusal by the session surfaces here, to the caller.
                more = await stream.MoveNextAsync().ConfigureAwait(false);
                admitted = true;
            }
            finally
            {
                if (!admitted)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
            }

            _turn = PumpAsync(stream, more, correlationId)
                .ContinueWith(settled => SettleTurn(settled, correlationId), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    /// <inheritdoc />
    public Task CancelTurnAsync(CancellationToken cancellationToken) => _session.CancelAsync(cancellationToken);

    /// <inheritdoc />
    public IAsyncEnumerable<OmpEvent> ReadEventsAsync(long afterSequence, CancellationToken cancellationToken) =>
        _log.FollowAsync(afterSequence, cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _closing.CancelAsync().ConfigureAwait(false);
        using var bound = new CancellationTokenSource(DisposeTimeout);
        try
        {
            // Senders waiting on the gate were cancelled above. Taking the gate waits out a sender in progress, and the gate
            // is never released again, so no send is admitted after this point.
            await _sendGate.WaitAsync(bound.Token).ConfigureAwait(false);

            // The cancelled turn's stream ends and its pump settles; the bound stops a stuck pump from hanging disposal.
            await _turn.WaitAsync(bound.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (bound.IsCancellationRequested)
        {
            throw new TimeoutException($"The adapter did not settle within {DisposeTimeout} at disposal.", ex);
        }
        finally
        {
            _log.Complete();
            await _session.DisposeAsync().ConfigureAwait(false);
            _sendGate.Dispose();
            _closing.Dispose();
        }
    }

    private async Task PumpAsync(IAsyncEnumerator<HarnessEvent> stream, bool more, string correlationId)
    {
        try
        {
            while (more)
            {
                _ = _log.Append(stream.Current, correlationId, _clock.GetUtcNow());
                more = await stream.MoveNextAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <remarks>
    /// Runs once the turn's pump has settled. A pump that did not complete is a failed turn: its fault is read here, which
    /// observes it, and a terminal <see cref="ErrorEvent"/> is appended and the log completed so that readers end. A turn
    /// cancelled by disposal appends nothing, because disposal completes the log itself. This method never throws.
    /// </remarks>
    private void SettleTurn(Task pump, string correlationId)
    {
        if (pump.IsCompletedSuccessfully)
        {
            return;
        }

        _faulted = true;
        _pumpFault = pump.Exception?.InnerException;
        if (_disposed)
        {
            return;
        }

        _ = _log.Append(new ErrorEvent("The turn ended before its event stream finished.", false), correlationId, _clock.GetUtcNow());
        _log.Complete();
    }
}

using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.InMemory;

/// <summary>
/// A session backed by an <see cref="InMemoryScript"/>. Prompt streams are delivered through a
/// bounded channel whose buffer is filled from the script before the consumer reads, so overflow
/// is deterministic. The session is single-prompt: a second submit while streaming is an illegal
/// transition.
/// </summary>
public sealed partial class InMemoryOmpSession : IOmpSession, IOmpModelControl, IOmpSessionOptions, IOmpSessionCommands, IOmpLiveSteering
{
    private const string CancelledReason = "cancelled";

    private readonly InMemoryScript _script;
    private readonly HarnessStateMachine _machine = new();
    private readonly System.Threading.Lock _gate = new();
    private bool _disposed;
    private bool _cancelRequested;

    internal InMemoryOmpSession(InMemoryScript script)
    {
        _script = script;
    }

    /// <summary>Gets the engine session identifier once the session has been created or resumed.</summary>
    public string? SessionId { get; private set; }

    /// <inheritdoc />
    public HarnessState State
    {
        get
        {
            lock (_gate)
            {
                return _disposed ? HarnessState.Disposed : _machine.State;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; } = FrozenDictionary<string, string>.Empty;

    /// <inheritdoc />
    public Task CreateAsync(CancellationToken cancellationToken) =>
        OpenAsync(Guid.NewGuid().ToString("N"), cancellationToken);

    /// <inheritdoc />
    public async Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await OpenAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
        string prompt,
        PromptOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);

        BeginStream();

        var run = new PromptRun();
        try
        {
            while (true)
            {
                var segment = Produce(run.Position, run.Completions);
                run.Position = segment.Resume;
                run.Completions = segment.Completions;
                await foreach (var evt in ReadSegmentAsync(segment.Events.Reader, run, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    yield return evt;
                }

                if (run.Cancelled)
                {
                    yield break;
                }

                if (run.Errored || segment.Pause is null)
                {
                    break;
                }

                await AwaitPermissionAsync(segment.Pause, cancellationToken).ConfigureAwait(false);
            }

            run.Finished = true;
        }
        finally
        {
            if (!run.Finished)
            {
                AbandonStreamIfActive();
            }
        }

        var trailing = FinishStream(run.SawTerminal);
        if (trailing is not null)
        {
            yield return trailing;
        }
    }

    /// <summary>Enters the streaming state for a new prompt.</summary>
    private void BeginStream()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _machine.TransitionTo(HarnessState.Streaming);
            _cancelRequested = false;
        }
    }

    /// <summary>Ends the stream for a cancellation observed mid-turn.</summary>
    /// <returns>The terminal event, or null when no cancellation was requested.</returns>
    private CompletedEvent? TakeCancellation()
    {
        if (!IsCancelRequested())
        {
            return null;
        }

        EndStream();
        return new CompletedEvent(CancelledReason);
    }

    /// <summary>Completes a stream that reached its end, throwing when no terminal event arrived.</summary>
    /// <param name="sawTerminal">Whether a completed or error event was delivered.</param>
    /// <returns>The trailing cancelled event, or null when the turn already ended terminally.</returns>
    private CompletedEvent? FinishStream(bool sawTerminal)
    {
        var cancelled = IsCancelRequested();
        if (!sawTerminal && !cancelled)
        {
            FaultStream();
            throw new DisconnectedHarnessException("Stream ended without a terminal event.", SessionId);
        }

        EndStream();
        return cancelled && !sawTerminal ? new CompletedEvent(CancelledReason) : null;
    }

    /// <inheritdoc />
    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_machine.State == HarnessState.Streaming)
            {
                _machine.TransitionTo(HarnessState.Cancelling);
                _cancelRequested = true;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            switch (_machine.State)
            {
                case HarnessState.Ready:
                case HarnessState.Faulted:
                    _machine.TransitionTo(HarnessState.Disposed);
                    break;
                case HarnessState.Streaming:
                case HarnessState.Cancelling:
                case HarnessState.Connecting:
                case HarnessState.Handshaking:
                    _machine.TransitionTo(HarnessState.Faulted);
                    _machine.TransitionTo(HarnessState.Disposed);
                    break;
                default:
                    // Never opened: nothing to tear down. State reports Disposed via the flag.
                    break;
            }
        }

        return ValueTask.CompletedTask;
    }

    private async Task OpenAsync(string sessionId, CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_machine.State is not (HarnessState.Disconnected or HarnessState.Faulted))
            {
                throw new InvalidHarnessTransitionException(_machine.State, HarnessState.Connecting, "CreateAsync and ResumeAsync require Disconnected or Faulted.");
            }

            _machine.TransitionTo(HarnessState.Connecting);
            if (_script.ProcessStartFailure is { } startFailure)
            {
                _machine.TransitionTo(HarnessState.Faulted);
                throw startFailure;
            }

            _machine.TransitionTo(HarnessState.Handshaking);
            if (_script.HandshakeFailure is { } handshakeFailure)
            {
                _machine.TransitionTo(HarnessState.Faulted);
                throw handshakeFailure;
            }

            _machine.TransitionTo(HarnessState.Ready);
            SessionId = sessionId;
        }
    }

    /// <summary>
    /// Replays the script from <paramref name="start"/> into a fresh bounded channel, and stops after a permission pause.
    /// A refused <see cref="ChannelWriter{T}.TryWrite(T)"/> is an overflow and completes the channel with
    /// <see cref="BackpressureHarnessException"/>; a second <see cref="CompletedEvent"/> completes it with
    /// <see cref="DuplicateCompletionHarnessException"/>.
    /// </summary>
    /// <param name="start">The index of the first step to replay.</param>
    /// <param name="completions">The completed events already emitted by earlier segments of this prompt.</param>
    private Segment Produce(int start, int completions)
    {
        var channel = Channel.CreateBounded<HarnessEvent>(new BoundedChannelOptions(_script.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
        var writer = channel.Writer;
        Segment Ended() => new(channel, null, _script.Steps.Count, completions);
        for (var index = start; index < _script.Steps.Count; index++)
        {
            var context = $"script step {index}";
            switch (_script.Steps[index])
            {
                case FailStep fail:
                    writer.TryComplete(fail.Error);
                    return Ended();
                case PermissionPauseStep pause:
                    if (!writer.TryWrite(pause.Request))
                    {
                        writer.TryComplete(new BackpressureHarnessException($"Event buffer of capacity {_script.ChannelCapacity} overflowed.", context));
                        return Ended();
                    }

                    writer.TryComplete();
                    return new Segment(channel, pause.Request, index + 1, completions);
                case EmitStep emit:
                    if (emit.Event is CompletedEvent)
                    {
                        completions++;
                        if (completions > 1)
                        {
                            writer.TryComplete(new DuplicateCompletionHarnessException("Script emitted more than one CompletedEvent.", context));
                            return Ended();
                        }
                    }

                    if (!writer.TryWrite(emit.Event))
                    {
                        writer.TryComplete(new BackpressureHarnessException($"Event buffer of capacity {_script.ChannelCapacity} overflowed.", context));
                        return Ended();
                    }

                    break;
            }
        }

        writer.TryComplete();
        return Ended();
    }

    /// <summary>
    /// Holds a paused stream until the script's resolver answers or its timeout elapses. The script models no tool effects,
    /// so the decision gates the stream but is not echoed into it. With no resolver, the pause ends at once without a grant.
    /// The timeout bounds the wait; a cancelled prompt does not interrupt it.
    /// </summary>
    private async Task AwaitPermissionAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
    {
        var resolver = _script.PermissionResolver;
        if (resolver is null)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_script.PermissionDecisionTimeout);
        try
        {
            await resolver.ResolveAsync(request, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The timeout elapsed without a decision: the pause ends without a grant.
        }
    }

    /// <summary>One replayed stretch of a script: its events, the pause that ends it (if any), and the step to resume at.</summary>
    private sealed record Segment(Channel<HarnessEvent> Events, PermissionRequestEvent? Pause, int Resume, int Completions);

    /// <summary>Mutable state of one prompt stream across its script segments.</summary>
    private sealed class PromptRun
    {
        public int Position { get; set; }

        public int Completions { get; set; }

        public bool SawTerminal { get; set; }

        public bool Errored { get; set; }

        public bool Cancelled { get; set; }

        public bool Finished { get; set; }
    }

    /// <summary>Yields one segment's events until it ends, an error ends the stream, or a cancellation is observed.</summary>
    private async IAsyncEnumerable<HarnessEvent> ReadSegmentAsync(ChannelReader<HarnessEvent> reader, PromptRun run, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await ReadNextAsync(reader, cancellationToken).ConfigureAwait(false) is { } next)
        {
            var cancelledEvent = TakeCancellation();
            if (cancelledEvent is not null)
            {
                run.Cancelled = true;
                run.Finished = true;
                yield return cancelledEvent;
                yield break;
            }

            run.SawTerminal |= next is CompletedEvent or ErrorEvent;
            yield return next;
            if (next is ErrorEvent)
            {
                run.Errored = true;
                yield break;
            }
        }
    }

    private async ValueTask<HarnessEvent?> ReadNextAsync(ChannelReader<HarnessEvent> reader, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw new DisconnectedHarnessException("The session was disposed while streaming.", SessionId);
            }
        }

        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (reader.TryRead(out var item))
                {
                    return item;
                }
            }

            return null;
        }
        catch (HarnessException)
        {
            FaultStream();
            throw;
        }
    }

    private bool IsCancelRequested()
    {
        lock (_gate)
        {
            return _cancelRequested;
        }
    }

    /// <summary>Completes a stream that reached its end normally (or was cancelled and already yielded).</summary>
    private void EndStream()
    {
        lock (_gate)
        {
            if (!_disposed && _machine.State is HarnessState.Streaming or HarnessState.Cancelling)
            {
                _machine.TransitionTo(HarnessState.Ready);
            }

            _cancelRequested = false;
        }
    }

    /// <summary>Marks an active stream as failed.</summary>
    private void FaultStream()
    {
        lock (_gate)
        {
            if (!_disposed && _machine.State is HarnessState.Streaming or HarnessState.Cancelling)
            {
                _machine.TransitionTo(HarnessState.Faulted);
            }
        }
    }

    /// <summary>Handles a consumer that stopped early (break, enumerator dispose, or token cancellation): cancel, then return to Ready.</summary>
    private void AbandonStreamIfActive()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_machine.State == HarnessState.Streaming)
            {
                _machine.TransitionTo(HarnessState.Cancelling);
            }

            if (_machine.State == HarnessState.Cancelling)
            {
                _machine.TransitionTo(HarnessState.Ready);
            }

            _cancelRequested = false;
        }
    }
}

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.Rpc;

/// <summary>
/// One omp RPC conversation over an <see cref="IRpcTransport"/>.
/// </summary>
/// <remarks>
/// <para>
/// A single pump task reads every frame. Command responses complete the command with the same id, and nothing else.
/// Responses with unknown ids are ignored. A <c>prompt</c> is admitted by its response (<c>success: true</c>); its
/// stream events flow to the prompt's slot until a <c>prompt_result</c> with the same id completes it. A response with
/// <c>agentInvoked: false</c> is a local command: no <c>prompt_result</c> follows, so the stream completes at once.
/// </para>
/// <para>
/// Any protocol violation (malformed frame, handshake timeout, dropped stream, event overflow) faults the session.
/// A command rejected by omp (<c>success: false</c>) does not fault it.
/// </para>
/// </remarks>
public sealed class OmpRpcSession : IOmpSession
{
    private const string Context = "omp rpc";
    private const string CompletedReason = "completed";
    private const string CancelledReason = "cancelled";
    private const string ErrorReason = "error";
    private const string AbortedStatus = "aborted";

    private readonly IRpcTransport _transport;
    private readonly TimeSpan _handshakeTimeout;
    private readonly TimeSpan _commandTimeout;
    private readonly HarnessStateMachine _machine = new();
    private readonly System.Threading.Lock _gate = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<RpcResponse>> _pending = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource<ReadyFrame> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly RpcChunkAssembler _assembler = new();
    private long _nextId;
    private PromptSlot? _active;
    private string? _lastCompletedPromptId;
    private HarnessException? _failure;
    private bool _disposed;
    private Task? _pump;
    private IReadOnlyDictionary<string, string> _capabilities = FrozenDictionary<string, string>.Empty;

    /// <summary>Creates a session over <paramref name="transport"/>. The transport is owned and disposed by the session.</summary>
    /// <param name="transport">The line transport, typically an <see cref="OmpRpcProcess"/>.</param>
    /// <param name="handshakeTimeout">How long to wait for the <c>ready</c> frame.</param>
    /// <param name="commandTimeout">How long to wait for a command's response or a prompt's completion.</param>
    public OmpRpcSession(IRpcTransport transport, TimeSpan handshakeTimeout, TimeSpan commandTimeout)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(handshakeTimeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(commandTimeout, TimeSpan.Zero);
        _transport = transport;
        _handshakeTimeout = handshakeTimeout;
        _commandTimeout = commandTimeout;
    }

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
    public IReadOnlyDictionary<string, string> AdvertisedCapabilities => _capabilities;

    /// <summary>
    /// Waits for the <c>ready</c> frame, negotiates protocol v2, and reads the engine state for its advertised
    /// capabilities.
    /// </summary>
    /// <param name="cancellationToken">Cancels the handshake.</param>
    /// <exception cref="HandshakeTimeoutHarnessException">omp did not send <c>ready</c> in time.</exception>
    public async Task CreateAsync(CancellationToken cancellationToken)
    {
        await HandshakeAsync(cancellationToken).ConfigureAwait(false);
        await RefreshCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        Move(HarnessState.Ready);
    }

    /// <summary>
    /// Completes the handshake, then switches the engine to an existing session file. omp addresses sessions by file
    /// path, so <paramref name="sessionId"/> is the session file path.
    /// </summary>
    /// <param name="sessionId">The path of the session file to switch to.</param>
    /// <param name="cancellationToken">Cancels the resume.</param>
    /// <exception cref="RpcCommandException">omp refused the switch.</exception>
    public async Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await HandshakeAsync(cancellationToken).ConfigureAwait(false);
        RpcResponse switched = await SendCommandAsync(
            new SwitchSessionCommand { Id = NextId(), SessionPath = sessionId },
            cancellationToken).ConfigureAwait(false);
        if (switched.Data is { ValueKind: JsonValueKind.Object } data
            && data.TryGetProperty("cancelled", out JsonElement cancelled)
            && cancelled.ValueKind == JsonValueKind.True)
        {
            throw Fail(new RpcCommandException("switch_session", "cancelled", "omp cancelled the session switch.", Context));
        }

        await RefreshCapabilitiesAsync(cancellationToken).ConfigureAwait(false);
        Move(HarnessState.Ready);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The prompt is admitted with a bounded timeout rather than the caller's token, so a cancellation cannot leave
    /// omp running a prompt the session believes was never sent. The token governs only the event consumer.
    /// </remarks>
    public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
        string prompt,
        PromptOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);

        PromptSlot slot = BeginPrompt();
        bool finished = false;
        try
        {
            RpcResponse admission = await SendCommandAsync(
                new PromptCommand
                {
                    Id = slot.Id,
                    Message = prompt,
                    StreamingBehavior = options.Steer ? "steer" : null,
                },
                CancellationToken.None).ConfigureAwait(false);
            slot.Admitted = true;

            if (admission.Data is { ValueKind: JsonValueKind.Object } data
                && data.TryGetProperty("agentInvoked", out JsonElement invoked)
                && invoked.ValueKind == JsonValueKind.False)
            {
                EndPrompt(slot);
                finished = true;
                yield return new CompletedEvent(CompletedReason);
                yield break;
            }

            ChannelReader<HarnessEvent> reader = slot.Events.Reader;
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out HarnessEvent? harnessEvent))
                {
                    yield return harnessEvent;
                }
            }

            finished = true;
        }
        finally
        {
            if (!finished)
            {
                await AbandonPromptAsync(slot).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_disposed || _failure is not null || _machine.State != HarnessState.Streaming)
            {
                return;
            }

            _machine.TransitionTo(HarnessState.Cancelling);
        }

        _ = await SendCommandAsync(new AbortCommand { Id = NextId() }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the engine's current state (<c>get_state</c>).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The state object omp returned.</returns>
    public async Task<JsonElement> GetStateAsync(CancellationToken cancellationToken)
    {
        RpcResponse response = await SendCommandAsync(new GetStateCommand { Id = NextId() }, cancellationToken)
            .ConfigureAwait(false);
        return response.Data ?? throw new MalformedFrameHarnessException("get_state returned no data.", Context);
    }

    /// <summary>Sends a command that has no typed method and returns its response data.</summary>
    /// <param name="createCommand">Builds the command from the id the session assigns.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The response data, or null when omp returned none.</returns>
    /// <exception cref="RpcCommandException">omp rejected the command.</exception>
    public async Task<JsonElement?> ExecuteAsync(Func<string, RpcCommand> createCommand, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(createCommand);
        RpcResponse response = await SendCommandAsync(createCommand(NextId()), cancellationToken).ConfigureAwait(false);
        return response.Data;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await _transport.DisposeAsync().ConfigureAwait(false);
        if (_pump is { } pump)
        {
            await pump.ConfigureAwait(false);
        }
    }

    private async Task HandshakeAsync(CancellationToken cancellationToken)
    {
        Move(HarnessState.Connecting);
        Move(HarnessState.Handshaking);
        _pump = PumpAsync();

        ReadyFrame ready = await AwaitReadyAsync(cancellationToken).ConfigureAwait(false);
        if (!ready.SupportedProtocolVersions.Contains(OmpRpcFrames.NegotiatedProtocolVersion))
        {
            throw Fail(new MalformedFrameHarnessException("omp does not support RPC protocol v2.", Context));
        }

        RpcResponse negotiated = await SendCommandAsync(
            new NegotiateProtocolCommand { Id = NextId(), ProtocolVersion = OmpRpcFrames.NegotiatedProtocolVersion },
            cancellationToken).ConfigureAwait(false);
        if (!(negotiated.Data is { } data
            && data.TryGetProperty("protocolVersion", out JsonElement version)
            && version.TryGetInt32(out int confirmed)
            && confirmed == OmpRpcFrames.NegotiatedProtocolVersion))
        {
            throw Fail(new MalformedFrameHarnessException("negotiate_protocol did not confirm protocol v2.", Context));
        }
    }

    private async Task<ReadyFrame> AwaitReadyAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_handshakeTimeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await _ready.Task.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw Fail(new HandshakeTimeoutHarnessException("omp did not send its ready frame in time.", Context));
        }
    }

    private async Task RefreshCapabilitiesAsync(CancellationToken cancellationToken)
    {
        JsonElement state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
        var capabilities = new Dictionary<string, string>(StringComparer.Ordinal);
        if (state.TryGetProperty("model", out JsonElement model) && model.ValueKind == JsonValueKind.Object)
        {
            AddString(capabilities, model, "id", "model.id");
            AddString(capabilities, model, "provider", "model.provider");
        }

        _capabilities = capabilities.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static void AddString(Dictionary<string, string> target, JsonElement source, string property, string key)
    {
        if (source.TryGetProperty(property, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.GetString() is { } text)
        {
            target[key] = text;
        }
    }

    private async Task<RpcResponse> SendCommandAsync(RpcCommand command, CancellationToken cancellationToken)
    {
        string id = command.Id ?? throw new ArgumentException("An RPC command must carry an id.", nameof(command));
        ThrowIfUnusable();

        var completion = new TaskCompletionSource<RpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = completion;
        try
        {
            await SendLineAsync(OmpRpcJsonContext.SerializeCommand(command), cancellationToken).ConfigureAwait(false);
            RpcResponse response = await completion.Task.WaitAsync(_commandTimeout, cancellationToken).ConfigureAwait(false);
            if (!response.Success)
            {
                throw new RpcCommandException(
                    response.Command ?? command.Type,
                    response.Code,
                    response.Error ?? "omp rejected the command.",
                    Context);
            }

            return response;
        }
        catch (TimeoutException)
        {
            throw Fail(new RpcCommandTimeoutException(command.Type, id, Context));
        }
        finally
        {
            _ = _pending.TryRemove(id, out _);
        }
    }

    private async Task SendLineAsync(string line, CancellationToken cancellationToken)
    {
        try
        {
            await _transport.SendLineAsync(line, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            throw Fail(new DisconnectedHarnessException("Writing to omp failed.", Context, ex));
        }
    }

    private async Task PumpAsync()
    {
        try
        {
            await foreach (string line in _transport.ReadLinesAsync(CancellationToken.None).ConfigureAwait(false))
            {
                Dispatch(line);
            }

            _ = Fail(new DisconnectedHarnessException("omp closed its output stream.", Context));
        }
        catch (HarnessException ex)
        {
            _ = Fail(ex);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _ = Fail(new DisconnectedHarnessException("Reading from omp failed.", Context, ex));
        }
    }

    private void Dispatch(string line)
    {
        using JsonDocument? document = _assembler.Accept(line);
        if (document is null)
        {
            return;
        }

        JsonElement frame = document.RootElement;
        switch (OmpRpcFrames.ReadType(frame))
        {
            case null:
                return;
            case OmpRpcFrames.ReadyType:
                _ = _ready.TrySetResult(Read(frame, OmpRpcJsonContext.Default.ReadyFrame));
                return;
            case OmpRpcFrames.ResponseType:
                CompleteCommand(Read(frame, OmpRpcJsonContext.Default.RpcResponse));
                return;
            case OmpRpcFrames.PromptResultType:
                CompletePrompt(Read(frame, OmpRpcJsonContext.Default.PromptResultFrame));
                return;
            case OmpRpcFrames.FrameErrorType:
                DispatchFrameError(Read(frame, OmpRpcJsonContext.Default.RpcFrameErrorFrame));
                return;
            case string type:
                DispatchStreamFrame(type, frame);
                return;
        }
    }

    private static T Read<T>(JsonElement frame, JsonTypeInfo<T> typeInfo)
    {
        try
        {
            return frame.Deserialize(typeInfo) ?? throw new MalformedFrameHarnessException("An omp frame is empty.", Context);
        }
        catch (JsonException ex)
        {
            throw new MalformedFrameHarnessException("An omp frame does not match its schema.", Context, ex);
        }
    }

    private void CompleteCommand(RpcResponse response)
    {
        if (response.Id is { } id && _pending.TryRemove(id, out TaskCompletionSource<RpcResponse>? completion))
        {
            _ = completion.TrySetResult(response);
        }
    }

    private void CompletePrompt(PromptResultFrame frame)
    {
        if (!IsKnownStatus(frame.Status))
        {
            throw Fail(new MalformedFrameHarnessException($"prompt_result has unknown status '{frame.Status}'.", Context));
        }

        PromptSlot? slot = ClaimActivePrompt(frame.Id);
        if (slot is null)
        {
            if (IsRepeatedCompletion(frame.Id))
            {
                throw Fail(new DuplicateCompletionHarnessException($"prompt_result repeated for id {frame.Id}.", Context));
            }

            return;
        }

        if (string.Equals(frame.Status, ErrorReason, StringComparison.Ordinal))
        {
            Publish(slot, new ErrorEvent(frame.Error?.Message ?? "omp reported a prompt error.", frame.Error?.Retryable ?? false));
        }

        Publish(slot, new CompletedEvent(ReasonFor(frame.Status)));
        _ = slot.Events.Writer.TryComplete();
    }

    private static bool IsKnownStatus(string status) =>
        string.Equals(status, CompletedReason, StringComparison.Ordinal)
        || string.Equals(status, AbortedStatus, StringComparison.Ordinal)
        || string.Equals(status, ErrorReason, StringComparison.Ordinal);

    private PromptSlot? ClaimActivePrompt(string? id)
    {
        lock (_gate)
        {
            if (_active is not { } active || !string.Equals(id, active.Id, StringComparison.Ordinal))
            {
                return null;
            }

            _active = null;
            _lastCompletedPromptId = active.Id;
            if (_machine.State is HarnessState.Streaming or HarnessState.Cancelling)
            {
                _machine.TransitionTo(HarnessState.Ready);
            }

            return active;
        }
    }

    private bool IsRepeatedCompletion(string? id)
    {
        lock (_gate)
        {
            return id is not null && string.Equals(id, _lastCompletedPromptId, StringComparison.Ordinal);
        }
    }

    private void DispatchFrameError(RpcFrameErrorFrame frame)
    {
        PromptSlot? slot;
        lock (_gate)
        {
            slot = _active;
        }

        if (slot is not null)
        {
            Publish(
                slot,
                new ErrorEvent(
                    $"omp dropped an oversized {frame.OriginalType ?? "frame"}: {frame.Error ?? "transport limit exceeded"}",
                    false));
        }
    }

    private void DispatchStreamFrame(string type, JsonElement frame)
    {
        PromptSlot? slot;
        lock (_gate)
        {
            slot = _active;
        }

        if (slot is not null && RpcEventMapper.Map(type, frame) is { } harnessEvent)
        {
            Publish(slot, harnessEvent);
        }
    }

    private void Publish(PromptSlot slot, HarnessEvent harnessEvent)
    {
        if (slot.Events.Writer.TryWrite(harnessEvent))
        {
            return;
        }

        HarnessException failure = Fail(
            new BackpressureHarnessException("omp produced events faster than the consumer read them.", Context));
        _ = slot.Events.Writer.TryComplete(failure);
        throw failure;
    }

    private PromptSlot BeginPrompt()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_failure is not null)
            {
                throw new DisconnectedHarnessException("The omp session has failed.", Context, _failure);
            }

            _machine.TransitionTo(HarnessState.Streaming);
            var slot = new PromptSlot(NextId());
            _active = slot;
            return slot;
        }
    }

    private void EndPrompt(PromptSlot slot)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_active, slot))
            {
                _active = null;
                if (_machine.State is HarnessState.Streaming or HarnessState.Cancelling)
                {
                    _machine.TransitionTo(HarnessState.Ready);
                }
            }
        }

        _ = slot.Events.Writer.TryComplete();
    }

    private async Task AbandonPromptAsync(PromptSlot slot)
    {
        bool live;
        lock (_gate)
        {
            live = ReferenceEquals(_active, slot);
            if (live && slot.Admitted && _machine.State == HarnessState.Streaming)
            {
                _machine.TransitionTo(HarnessState.Cancelling);
            }
        }

        if (!live)
        {
            return;
        }

        if (!slot.Admitted)
        {
            EndPrompt(slot);
            return;
        }

        // Admitted but abandoned: abort the engine's turn, then wait for its prompt_result so the next prompt is admitted.
        try
        {
            _ = await SendCommandAsync(new AbortCommand { Id = NextId() }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (RpcCommandException)
        {
            // omp had no run to abort; a prompt_result for the admitted prompt still follows.
        }

        Task drained = slot.Events.Reader.Completion;
        Task finished = await Task.WhenAny(drained, Task.Delay(_commandTimeout)).ConfigureAwait(false);
        if (!ReferenceEquals(finished, drained))
        {
            throw Fail(new RpcCommandTimeoutException("prompt_result", slot.Id, Context));
        }
    }

    private static string ReasonFor(string status) => status switch
    {
        CompletedReason => CompletedReason,
        AbortedStatus => CancelledReason,
        _ => ErrorReason,
    };

    private void Move(HarnessState target)
    {
        lock (_gate)
        {
            _machine.TransitionTo(target);
        }
    }

    private void ThrowIfUnusable()
    {
        HarnessException? failure;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            failure = _failure;
        }

        if (failure is not null)
        {
            throw new DisconnectedHarnessException("The omp session has failed.", Context, failure);
        }
    }

    /// <summary>Records the first failure, faults the state machine, and releases every waiter with it.</summary>
    private HarnessException Fail(HarnessException exception)
    {
        PromptSlot? slot;
        lock (_gate)
        {
            _failure ??= exception;
            if (HarnessStateMachine.IsAllowed(_machine.State, HarnessState.Faulted))
            {
                _machine.TransitionTo(HarnessState.Faulted);
            }

            slot = _active;
            _active = null;
        }

        foreach (KeyValuePair<string, TaskCompletionSource<RpcResponse>> waiter in _pending)
        {
            if (_pending.TryRemove(waiter.Key, out _))
            {
                _ = waiter.Value.TrySetException(exception);
            }
        }

        _ = _ready.TrySetException(exception);
        _ = slot?.Events.Writer.TryComplete(exception);
        return exception;
    }

    private string NextId() => Interlocked.Increment(ref _nextId).ToString(CultureInfo.InvariantCulture);
}

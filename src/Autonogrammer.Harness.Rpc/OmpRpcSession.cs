using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Rpc;

/// <summary>
/// One omp RPC conversation over an <see cref="IRpcTransport"/>.
/// </summary>
/// <remarks>
/// <para>
/// A single pump task reads every frame. Command responses complete the command with the same id, and nothing else.
/// Command responses with unknown ids are ignored. A <c>prompt</c> is admitted by its response (<c>success: true</c>);
/// its stream events flow to the prompt's slot until a <c>prompt_result</c> with the same id completes it. A
/// <c>prompt_result</c> for any other id faults the session: an exact repeat of the last completed id is a duplicate
/// completion, anything else is malformed. A response with <c>agentInvoked: false</c> is a local command: no
/// <c>prompt_result</c> follows, so the stream completes at once.
/// </para>
/// <para>
/// Any protocol violation (malformed frame, handshake timeout, dropped stream, event overflow) faults the session.
/// A command rejected by omp (<c>success: false</c>) does not fault it.
/// </para>
/// </remarks>
public sealed class OmpRpcSession
    : IOmpSession, IOmpLiveSteering, IOmpSessionIdentity, IOmpSessionCommands, IOmpModelControl, IOmpSessionOptions
{
    private const string Context = "omp rpc";
    private const string CompletedReason = "completed";
    private const string CancelledReason = "cancelled";
    private const string ErrorReason = "error";
    private const string AbortedStatus = "aborted";
    private const int MaxSteerBytes = 8192;
    private const string ModelSwitchGap =
        "omp RPC exposes no verified model-switch command (OQ-523); the model is fixed at engine start (--model/profile).";
    private const string OptionSetGap =
        "omp RPC exposes no verified option-set command (OQ-523); session options are fixed at engine start.";

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
    private ReadyFrame? _readyFrame;
    private bool _handshakeCompleted;
    private string? _nativeSessionId;
    private string? _currentModelId;
    private IReadOnlyList<OmpModelChoice> _modelChoices = [];
    private IReadOnlyList<OmpCommand> _commands = [];

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

    /// <inheritdoc />
    /// <remarks>
    /// The resume key omp reported as <c>get_state.sessionFile</c>, or <see langword="null"/> when it reported
    /// none (an ephemeral <c>--no-session</c> profile has no file; the value is never synthesised).
    /// </remarks>
    public string? NativeSessionId
    {
        get
        {
            lock (_gate)
            {
                return _nativeSessionId;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<OmpCommand> Commands
    {
        get
        {
            lock (_gate)
            {
                return _commands;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? CommandsChanged;

    /// <inheritdoc />
    /// <remarks>Exactly the current model when omp reported one; RPC advertises no model list to choose from.</remarks>
    public IReadOnlyList<OmpModelChoice> ModelChoices
    {
        get
        {
            lock (_gate)
            {
                return _modelChoices;
            }
        }
    }

    /// <inheritdoc />
    public string? CurrentModelId
    {
        get
        {
            lock (_gate)
            {
                return _currentModelId;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? ModelChanged;

    /// <inheritdoc />
    /// <remarks>
    /// Always empty: RPC reports no selectable option list with a verified set path (OQ-523), so none is
    /// fabricated from <c>get_state</c> data such as <c>thinkingLevels</c>.
    /// </remarks>
    public IReadOnlyList<OmpSessionOption> Options => [];

    /// <inheritdoc />
    /// <remarks>Never raised: <see cref="Options"/> is an explicitly empty surface (OQ-523).</remarks>
    public event EventHandler? OptionsChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    /// <remarks>RPC steering is protocol-native, so this is true once the handshake completed and remains so until disposal or fault.</remarks>
    public bool IsSupported
    {
        get
        {
            lock (_gate)
            {
                return _handshakeCompleted && !_disposed && _failure is null;
            }
        }
    }

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
    /// <remarks>
    /// Cross-transport warning: session identities are not interchangeable between transports. An
    /// ACP-advertised session id is not an RPC resume key, so persisted ids must only be resumed on the
    /// transport that issued them; resuming a foreign id here surfaces omp's <c>switch_session</c> refusal
    /// as an <see cref="RpcCommandException"/>, never a silent fresh session.
    /// </remarks>
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
    /// <see cref="PromptOptions.Images"/> are mapped onto the <c>prompt</c> frame (the verified-18.12.6 shape).
    /// <see cref="PromptOptions.Resources"/> have no observed RPC equivalent: a prompt carrying them sends
    /// nothing and ends the turn with an <see cref="ErrorEvent"/> naming that gap, never silently dropping them.
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
            if (options.Resources.Count > 0)
            {
                EndPrompt(slot);
                finished = true;
                yield return new ErrorEvent(
                    "omp RPC has no observed embedded-resource prompt shape; the prompt was not sent.", false);
                yield break;
            }

            RpcResponse admission = await SendCommandAsync(
                new PromptCommand
                {
                    Id = slot.Id,
                    Message = prompt,
                    Images = MapImages(options.Images),
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

    /// <inheritdoc />
    /// <remarks>
    /// Sends a <c>prompt</c> with <c>streamingBehavior</c> (<c>steer</c> or <c>followUp</c>) and awaits admission
    /// only: no prompt slot is allocated and the running turn's stream delivers the effect. A refusal sends
    /// nothing. An admission answered <c>agentInvoked: false</c> took no turn and maps to
    /// <see cref="OmpSteerOutcome.PromptRequired"/>; a rejection, a transport failure, or the turn ending
    /// while the steer was in flight maps to <see cref="OmpSteerOutcome.Failed"/> and never throws across
    /// the running stream.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="queue"/> is not a defined value.</exception>
    public async Task<OmpSteerResult> SteerAsync(string text, OmpSteerLane queue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!Enum.IsDefined(queue))
        {
            throw new ArgumentOutOfRangeException(nameof(queue));
        }

        if (SteerRefusal(text, queue, out PromptSlot? slot) is { } refusal)
        {
            return refusal;
        }

        PromptSlot running = slot!;

        RpcResponse admission;
        try
        {
            admission = await SendCommandAsync(
                new PromptCommand
                {
                    Id = NextId(),
                    Message = text,
                    StreamingBehavior = queue == OmpSteerLane.FollowUp ? "followUp" : "steer",
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (HarnessException ex)
        {
            return new OmpSteerResult(OmpSteerOutcome.Failed, queue, ex.Message);
        }

        lock (_gate)
        {
            if (!ReferenceEquals(_active, running))
            {
                return new OmpSteerResult(OmpSteerOutcome.Failed, queue, "The turn ended while the message was sent.");
            }
        }

        if (admission.Data is { ValueKind: JsonValueKind.Object } data
            && data.TryGetProperty("agentInvoked", out JsonElement invoked)
            && invoked.ValueKind == JsonValueKind.False)
        {
            return new OmpSteerResult(OmpSteerOutcome.PromptRequired, queue, null);
        }

        return new OmpSteerResult(OmpSteerOutcome.Injected, queue, null);
    }

    /// <summary>
    /// Returns the refusal for a steer the session cannot take, or null (with the streaming prompt's slot in
    /// <paramref name="running"/>) when a turn is streaming and the text passes validation. Every refusal is
    /// decided before anything is sent.
    /// </summary>
    private OmpSteerResult? SteerRefusal(string text, OmpSteerLane queue, out PromptSlot? running)
    {
        lock (_gate)
        {
            running = _active;
            if (!_handshakeCompleted)
            {
                return new OmpSteerResult(OmpSteerOutcome.Unsupported, queue, "The session is not open.");
            }

            if (_disposed || _failure is not null || _machine.State != HarnessState.Streaming || running is null)
            {
                return new OmpSteerResult(OmpSteerOutcome.NotRunning, queue, "No turn is running.");
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new OmpSteerResult(OmpSteerOutcome.Failed, queue, "The message is empty.");
        }

        return Encoding.UTF8.GetByteCount(text) > MaxSteerBytes
            ? new OmpSteerResult(OmpSteerOutcome.Failed, queue, "The message is too long: the limit is 8 KiB.")
            : null;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends nothing: omp RPC exposes no verified model-switch command (OQ-523), so every change is refused
    /// with that reason. Asking for the already-current model is a no-op switch, matching ACP.
    /// </remarks>
    public Task<OmpModelSwitchResult> SetModelAsync(string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        string? current = CurrentModelId;
        return Task.FromResult(string.Equals(value, current, StringComparison.Ordinal)
            ? new OmpModelSwitchResult(OmpModelSwitchOutcome.Switched, current, null)
            : new OmpModelSwitchResult(OmpModelSwitchOutcome.NoModelOption, current, ModelSwitchGap));
    }

    /// <inheritdoc />
    /// <remarks>Sends nothing: no verified option-set command exists (OQ-523); every option is unknown here.</remarks>
    public Task<OmpOptionSetResult> SetOptionAsync(string configId, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configId);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new OmpOptionSetResult(OmpOptionSetOutcome.UnknownOption, null, OptionSetGap));
    }

    private static List<RpcImageContent>? MapImages(IReadOnlyList<PromptImage> images) =>
        images.Count == 0
            ? null
            : [.. images.Select(static image => new RpcImageContent
            {
                Data = Convert.ToBase64String(image.Data.Span),
                MimeType = image.MimeType,
            })];

    /// <summary>
    /// Aborts the in-flight turn and withdraws queued steering and follow-up input back to the caller.
    /// The plain abort-on-cancel path is untouched: <see cref="CancelAsync"/> still sends <c>abort</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The steering and follow-up inputs omp restored to the caller.</returns>
    /// <exception cref="RpcCommandException">omp rejected the abort.</exception>
    /// <exception cref="MalformedFrameHarnessException">omp answered without a queue object.</exception>
    public async Task<RestoredQueue> AbortAndRestoreQueueAsync(CancellationToken cancellationToken)
    {
        RpcResponse response = await SendCommandAsync(new AbortAndRestoreQueueCommand { Id = NextId() }, cancellationToken)
            .ConfigureAwait(false);
        return ParseRestoredQueue(response.Data);
    }

    private static RestoredQueue ParseRestoredQueue(JsonElement? data)
    {
        if (data is not { ValueKind: JsonValueKind.Object } root)
        {
            throw new MalformedFrameHarnessException("abort_and_restore_queue returned no queue data.", Context);
        }

        return new RestoredQueue
        {
            Steering = ReadQueuedInputs(root, "steering"),
            FollowUp = ReadQueuedInputs(root, "followUp"),
        };
    }

    private static List<QueuedInput> ReadQueuedInputs(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out JsonElement array)
            || array.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new MalformedFrameHarnessException($"abort_and_restore_queue data '{property}' is not an array.", Context);
        }

        var inputs = new List<QueuedInput>(array.GetArrayLength());
        foreach (JsonElement item in array.EnumerateArray())
        {
            inputs.Add(ReadQueuedInput(item, property));
        }

        return inputs;
    }

    private static QueuedInput ReadQueuedInput(JsonElement item, string property)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("text", out JsonElement text)
            || text.ValueKind != JsonValueKind.String
            || text.GetString() is not { } content)
        {
            throw new MalformedFrameHarnessException(
                $"abort_and_restore_queue data '{property}' holds an item without text.", Context);
        }

        List<RpcImageContent>? images = null;
        if (item.TryGetProperty("images", out JsonElement imagesElement)
            && imagesElement.ValueKind != JsonValueKind.Null)
        {
            if (imagesElement.ValueKind != JsonValueKind.Array)
            {
                throw new MalformedFrameHarnessException(
                    $"abort_and_restore_queue data '{property}' holds images that are not an array.", Context);
            }

            images = new List<RpcImageContent>(imagesElement.GetArrayLength());
            foreach (JsonElement image in imagesElement.EnumerateArray())
            {
                images.Add(ReadQueuedImage(image, property));
            }
        }

        return new QueuedInput { Text = content, Images = images };
    }

    private static RpcImageContent ReadQueuedImage(JsonElement image, string property)
    {
        if (image.ValueKind != JsonValueKind.Object
            || !image.TryGetProperty("data", out JsonElement data)
            || data.ValueKind != JsonValueKind.String
            || data.GetString() is not { } bytes
            || !image.TryGetProperty("mimeType", out JsonElement mimeType)
            || mimeType.ValueKind != JsonValueKind.String
            || mimeType.GetString() is not { } type)
        {
            throw new MalformedFrameHarnessException(
                $"abort_and_restore_queue data '{property}' holds an image without data or mimeType.", Context);
        }

        return new RpcImageContent { Data = bytes, MimeType = type };
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
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "HI-01: observing the pump must not throw its recorded failure out of disposal.")]
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
            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // The session already recorded its failure; disposal must not throw it back out.
            }
        }
    }

    private async Task HandshakeAsync(CancellationToken cancellationToken)
    {
        Move(HarnessState.Connecting);
        Move(HarnessState.Handshaking);
        _pump = PumpAsync();

        ReadyFrame ready = await AwaitReadyAsync(cancellationToken).ConfigureAwait(false);
        _readyFrame = ready;
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

        lock (_gate)
        {
            _handshakeCompleted = true;
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
            throw Fail(new HandshakeTimeoutHarnessException(WithStderrTail("omp did not send its ready frame in time."), Context));
        }
    }

    /// <summary>
    /// Appends a bounded tail of the engine's recent stderr to a start-failure message when the transport is
    /// the real process, so a pre-<c>ready</c> exit (for example the ledger's no-default-model exit) names
    /// itself instead of surfacing as a blank timeout. Fake transports keep the bare message. Only the last
    /// few non-empty lines are used, capped, never the whole 200-line tail.
    /// </summary>
    private string WithStderrTail(string message)
    {
        if (_transport is not OmpRpcProcess process)
        {
            return message;
        }

        string[] lines = [.. process.RecentStderr
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(5)
            .Select(static line => line.Trim())];
        if (lines.Length == 0)
        {
            return message;
        }

        string tail = string.Join(" | ", lines);
        if (tail.Length > 1000)
        {
            tail = tail[..1000];
        }

        return string.Concat(message, " Engine stderr: ", tail);
    }

    private async Task RefreshCapabilitiesAsync(CancellationToken cancellationToken)
    {
        JsonElement state = await GetStateAsync(cancellationToken).ConfigureAwait(false);
        var capabilities = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["transport"] = "rpc",
            ["approvals"] = "denied",
        };
        string? modelId = null;
        string? modelName = null;
        if (state.TryGetProperty("model", out JsonElement model) && model.ValueKind == JsonValueKind.Object)
        {
            AddString(capabilities, model, "id", "model.id");
            AddString(capabilities, model, "provider", "model.provider");
            modelId = ReadString(model, "id");
            modelName = ReadString(model, "name");
        }

        if (_readyFrame is { } readyFrame)
        {
            capabilities["ready.supportsBashExcludeFromContext"] =
                readyFrame.SupportsBashExcludeFromContext ? "true" : "false";
        }

        string? sessionFile = ReadString(state, "sessionFile");
        bool modelChanged;
        lock (_gate)
        {
            _capabilities = capabilities.ToFrozenDictionary(StringComparer.Ordinal);
            _nativeSessionId = sessionFile;
            modelChanged = !string.Equals(modelId, _currentModelId, StringComparison.Ordinal);
            _currentModelId = modelId;
            _modelChoices = modelId is null ? [] : [new OmpModelChoice(modelId, modelName ?? modelId)];
        }

        if (modelChanged)
        {
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string? ReadString(JsonElement source, string property) =>
        source.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

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

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "HI-01: unexpected transport failures must end the session typed, never escape raw.")]
    private async Task PumpAsync()
    {
        try
        {
            await foreach (string line in _transport.ReadLinesAsync(CancellationToken.None).ConfigureAwait(false))
            {
                Dispatch(line);
            }

            _ = Fail(new DisconnectedHarnessException(WithStderrTail("omp closed its output stream."), Context));
        }
        catch (HarnessException ex)
        {
            _ = Fail(ex);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            _ = Fail(new DisconnectedHarnessException("Reading from omp failed.", Context, ex));
        }
        catch (Exception ex)
        {
            // Harness failures are claimed above; anything else is an unexpected transport or dispatch
            // failure that must end the session with a typed error instead of hanging its consumers.
            _ = Fail(new DisconnectedHarnessException("The omp session failed.", Context, ex));
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

            throw Fail(new MalformedFrameHarnessException($"prompt_result for unknown prompt id '{frame.Id}'.", Context));
        }

        if (string.Equals(frame.Status, ErrorReason, StringComparison.Ordinal))
        {
            Publish(slot, new ErrorEvent(frame.Error?.Message ?? "omp reported a prompt error.", frame.Error?.Retryable ?? false));
        }
        else
        {
            Publish(slot, new CompletedEvent(ReasonFor(frame.Status)));
        }

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
                new DiagnosticEvent(
                    $"omp dropped an oversized {frame.OriginalType ?? "frame"}: {frame.Error ?? "transport limit exceeded"}"));
        }
    }

    private void DispatchStreamFrame(string type, JsonElement frame)
    {
        if (string.Equals(type, "available_commands_update", StringComparison.Ordinal))
        {
            ApplyAvailableCommands(frame);
            return;
        }

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

    /// <summary>
    /// Applies one unsolicited <c>available_commands_update</c> frame (observed-19.1.1, frozen capture seq 3)
    /// as a whole-list replacement in engine order, raising <see cref="CommandsChanged"/> only when the list
    /// differs. Malformed entries are skipped per entry and a malformed frame is ignored: an unsolicited
    /// frame must never fault the session (R-UNSOLICITED).
    /// </summary>
    private void ApplyAvailableCommands(JsonElement frame)
    {
        if (!frame.TryGetProperty("commands", out JsonElement entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var commands = new List<OmpCommand>();
        foreach (JsonElement entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || ReadString(entry, "name") is not { } name)
            {
                continue;
            }

            string? hint = entry.TryGetProperty("input", out JsonElement input) && input.ValueKind == JsonValueKind.Object
                ? ReadString(input, "hint")
                : null;
            commands.Add(new OmpCommand(name, ReadString(entry, "description"), hint));
        }

        bool changed;
        lock (_gate)
        {
            changed = !_commands.SequenceEqual(commands);
            if (changed)
            {
                _commands = commands;
            }
        }

        if (changed)
        {
            CommandsChanged?.Invoke(this, EventArgs.Empty);
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

        // Admitted but abandoned: abort the engine's turn, then drain its events until prompt_result closes the slot, so the next prompt is admitted.
        try
        {
            _ = await SendCommandAsync(new AbortCommand { Id = NextId() }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (RpcCommandException)
        {
            // omp had no run to abort; a prompt_result for the admitted prompt still follows.
        }

        Task drained = DiscardUntilCompleteAsync(slot.Events.Reader);
        Task finished = await Task.WhenAny(drained, Task.Delay(_commandTimeout)).ConfigureAwait(false);
        if (!ReferenceEquals(finished, drained))
        {
            throw Fail(new RpcCommandTimeoutException("prompt_result", slot.Id, Context));
        }
    }

    /// <summary>Reads and discards a slot's events until its writer completes. An abandoned consumer must not leave the terminal unread.</summary>
    private static async Task DiscardUntilCompleteAsync(ChannelReader<HarnessEvent> reader)
    {
        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            _ = reader.TryRead(out _);
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

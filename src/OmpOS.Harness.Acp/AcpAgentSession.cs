using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp.AcpProtocol;

namespace OmpOS.Harness.Acp;

/// <summary>
/// An omp conversation over ACP v1. One pump task owns every inbound frame: it correlates responses by id, routes
/// <c>session/update</c> notifications into the active prompt stream, and answers agent permission requests through
/// <see cref="AcpApprovalPolicy"/>. Because the pump is the only reader, events arrive in wire order and every state
/// change happens at a known point in that order.
/// </summary>
public sealed class AcpAgentSession : IOmpSession
{
    private const string UpdateMethod = "session/update";
    private const string PermissionMethod = "session/request_permission";
    private const int MethodNotFound = -32601;
    private const int InvalidParams = -32602;

    private static readonly string[] AllowKinds = ["allow_once", "allow_always"];
    private static readonly string[] RejectKinds = ["reject_once", "reject_always"];

    private readonly IAcpTransport _transport;
    private readonly AcpClientOptions _options;
    private readonly HarnessStateMachine _machine = new();
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<long, PendingCall> _pending = [];
    private long _lastRequestId;
    private PromptTurn? _turn;
    private Task? _pumpTask;
    private bool _connectionLost;
    private bool _sessionClosed;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="AcpAgentSession"/> class.</summary>
    /// <param name="transport">The transport to the agent. The session opens it; the session closes it.</param>
    /// <param name="options">Session options.</param>
    public AcpAgentSession(IAcpTransport transport, AcpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        _transport = transport;
        _options = options;
    }

    /// <summary>Gets the ACP session identifier once the session has been created or resumed.</summary>
    public string? SessionId { get; private set; }

    /// <summary>Gets the agent identity reported by <c>initialize</c>.</summary>
    public AcpFrames.AgentInfo? AgentInfo { get; private set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; private set; } = FrozenDictionary<string, string>.Empty;

    /// <inheritdoc />
    public HarnessState State
    {
        get
        {
            lock (_gate)
            {
                return _disposed || _sessionClosed ? HarnessState.Disposed : _machine.State;
            }
        }
    }

    /// <inheritdoc />
    public Task CreateAsync(CancellationToken cancellationToken) => OpenAsync(null, cancellationToken);

    /// <inheritdoc />
    public Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return OpenAsync(sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
        string prompt,
        PromptOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Steer)
        {
            throw new NotSupportedException("ACP v1 has no steer operation. Cancel the in-flight prompt, then submit.");
        }

        var turn = BeginTurn();
        var finished = false;
        try
        {
            await StartPromptAsync(turn, prompt, cancellationToken).ConfigureAwait(false);
            await foreach (var harnessEvent in turn.Events.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                yield return harnessEvent;
            }

            finished = true;
        }
        finally
        {
            if (!finished)
            {
                await AbandonTurnAsync(turn).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async Task CancelAsync(CancellationToken cancellationToken)
    {
        PromptTurn turn;
        lock (_gate)
        {
            var active = _turn;
            if (_disposed || _machine.State != HarnessState.Streaming || active is null)
            {
                return;
            }

            _machine.TransitionTo(HarnessState.Cancelling);
            turn = active;
        }

        await SendCancelAsync(turn, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ends the session on the agent with <c>session/close</c>. Disposing the session does this best-effort.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="AcpRequestFailedHarnessException">The agent answered <c>session/close</c> with an error or timed out.</exception>
    /// <exception cref="InvalidOperationException">A prompt is in flight.</exception>
    public async Task CloseSessionAsync(CancellationToken cancellationToken)
    {
        string sessionId;
        lock (_gate)
        {
            if (_disposed || _sessionClosed || SessionId is null)
            {
                return;
            }

            if (_machine.State is HarnessState.Streaming or HarnessState.Cancelling)
            {
                throw new InvalidOperationException("The ACP session cannot close while a prompt is in flight.");
            }

            sessionId = SessionId;
        }

        await CloseRemoteAsync(sessionId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        PromptTurn? turn;
        string? sessionId;
        bool closeRemote;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            turn = _turn;
            sessionId = SessionId;
            closeRemote = sessionId is not null && !_sessionClosed && !_connectionLost;
        }

        if (turn is not null)
        {
            FailTurn(turn, new DisconnectedHarnessException("The ACP session was disposed while a prompt was streaming.", sessionId));
        }

        if (closeRemote && sessionId is not null)
        {
            try
            {
                await CloseRemoteAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (HarnessException)
            {
                // Best-effort: the transport is torn down below regardless of the agent's answer.
            }
        }

        await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        if (_pumpTask is { } pump)
        {
            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (HarnessException)
            {
                // The pump records connection loss itself; disposal has nothing further to report.
            }
        }
    }

    private static void ValidateOptions(AcpClientOptions options)
    {
        if (options.HandshakeTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("The handshake timeout must be positive.", nameof(options));
        }

        if (options.RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("The request timeout must be positive.", nameof(options));
        }

        if (!Path.IsPathFullyQualified(options.WorkingDirectory))
        {
            throw new ArgumentException("ACP requires an absolute working directory.", nameof(options));
        }
    }

    private async Task OpenAsync(string? resumeSessionId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_pumpTask is not null)
            {
                throw new InvalidOperationException("The ACP session is already open; create a new session instead.");
            }

            _machine.TransitionTo(HarnessState.Connecting);
        }

        var opened = false;
        try
        {
            await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _machine.TransitionTo(HarnessState.Handshaking);
            }

            _pumpTask = PumpAsync();
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
            await OpenSessionAsync(resumeSessionId, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _machine.TransitionTo(HarnessState.Ready);
            }

            opened = true;
        }
        finally
        {
            if (!opened)
            {
                await FailOpenAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var parameters = new AcpFrames.InitializeParams(
            AcpFrames.ProtocolVersion,
            new AcpFrames.ClientCapabilities(new AcpFrames.FsCapabilities(ReadTextFile: false, WriteTextFile: false)));
        AcpFrames.InitializeResult result;
        try
        {
            var response = await CallAsync("initialize", parameters, AcpJsonContext.Default.RequestInitializeParams, _options.HandshakeTimeout, cancellationToken)
                .ConfigureAwait(false);
            result = ReadResult(response, AcpJsonContext.Default.InitializeResult, "initialize");
        }
        catch (AcpRequestFailedHarnessException ex) when (ex.TimedOut)
        {
            throw new HandshakeTimeoutHarnessException("The omp acp initialize request timed out.", "initialize", ex);
        }

        if (result.ProtocolVersion != AcpFrames.ProtocolVersion)
        {
            throw new AcpRequestFailedHarnessException(
                $"omp acp selected ACP protocol version {result.ProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)}; this client speaks version {AcpFrames.ProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)}.",
                "initialize");
        }

        lock (_gate)
        {
            AgentInfo = result.AgentInfo;
            AdvertisedCapabilities = AcpWireMapping.Flatten(result.AgentCapabilities);
        }
    }

    private async Task OpenSessionAsync(string? resumeSessionId, CancellationToken cancellationToken)
    {
        var cwd = _options.WorkingDirectory;
        if (resumeSessionId is null)
        {
            var response = await CallAsync(
                "session/new",
                new AcpFrames.NewSessionParams(cwd, []),
                AcpJsonContext.Default.RequestNewSessionParams,
                _options.RequestTimeout,
                cancellationToken).ConfigureAwait(false);
            var created = ReadResult(response, AcpJsonContext.Default.NewSessionResult, "session/new");
            if (created.SessionId.Length == 0)
            {
                throw new AcpRequestFailedHarnessException("omp acp created a session without a sessionId.", "session/new");
            }

            SetSessionId(created.SessionId);
            return;
        }

        await CallAsync(
            "session/load",
            new AcpFrames.LoadSessionParams(resumeSessionId, cwd, []),
            AcpJsonContext.Default.RequestLoadSessionParams,
            _options.RequestTimeout,
            cancellationToken).ConfigureAwait(false);
        SetSessionId(resumeSessionId);
    }

    private async Task CloseRemoteAsync(string sessionId, CancellationToken cancellationToken)
    {
        await CallAsync(
            "session/close",
            new AcpFrames.CloseSessionParams(sessionId),
            AcpJsonContext.Default.RequestCloseSessionParams,
            _options.RequestTimeout,
            cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _sessionClosed = true;
        }
    }

    private async Task<AcpFrames.Inbound> CallAsync<TParams>(
        string method,
        TParams parameters,
        JsonTypeInfo<AcpFrames.Request<TParams>> typeInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var response = new TaskCompletionSource<AcpFrames.Inbound>(TaskCreationOptions.RunContinuationsAsynchronously);
        long id;
        lock (_gate)
        {
            ThrowIfConnectionLost();
            id = ++_lastRequestId;
            _pending[id] = new PendingCall(frame => response.TrySetResult(frame), error => response.TrySetException(error));
        }

        try
        {
            await SendFrameAsync(new AcpFrames.Request<TParams>(id, method, parameters), typeInfo, cancellationToken).ConfigureAwait(false);
            var inbound = await AwaitResponseAsync(response.Task, method, timeout, cancellationToken).ConfigureAwait(false);
            if (inbound.Error is { } error)
            {
                throw new AcpRequestFailedHarnessException(error.Message, method, error.Code, timedOut: false);
            }

            return inbound;
        }
        finally
        {
            lock (_gate)
            {
                _pending.Remove(id);
            }
        }
    }

    private static async Task<AcpFrames.Inbound> AwaitResponseAsync(Task<AcpFrames.Inbound> response, string method, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        try
        {
            return await response.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AcpRequestFailedHarnessException($"The ACP request '{method}' timed out.", method, errorCode: null, timedOut: true);
        }
    }

    private async Task SendFrameAsync<T>(T frame, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame, typeInfo);
        await _transport.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task PumpAsync()
    {
        Exception failure = new DisconnectedHarnessException("omp acp closed the stream.", "acp");
        try
        {
            await foreach (var frame in _transport.ReceiveAsync(CancellationToken.None).WithCancellation(CancellationToken.None).ConfigureAwait(false))
            {
                await DispatchAsync(frame).ConfigureAwait(false);
            }
        }
        catch (HarnessException ex)
        {
            failure = ex;
        }
        finally
        {
            OnConnectionLost(failure);
        }
    }

    private async Task DispatchAsync(ReadOnlyMemory<byte> frame)
    {
        var inbound = ParseFrame(frame);
        if (inbound.Method is null)
        {
            if (!TryGetId(inbound.Id, out var responseId))
            {
                throw AcpFrameDiagnostics.Malformed(frame, "response without a numeric id");
            }

            PendingCall? call;
            lock (_gate)
            {
                _pending.Remove(responseId, out call);
            }

            call?.OnResponse(inbound);
            return;
        }

        if (inbound.Id is null)
        {
            if (string.Equals(inbound.Method, UpdateMethod, StringComparison.Ordinal))
            {
                HandleSessionUpdate(inbound);
            }

            return;
        }

        if (!TryGetId(inbound.Id, out var requestId))
        {
            throw AcpFrameDiagnostics.Malformed(frame, "agent request without a numeric id");
        }

        await HandleAgentRequestAsync(requestId, inbound).ConfigureAwait(false);
    }

    private void HandleSessionUpdate(AcpFrames.Inbound inbound)
    {
        var turn = ActiveTurn();
        if (turn is null)
        {
            // Updates outside a prompt (for example history replay during session/load) have no consumer.
            return;
        }

        try
        {
            var notification = inbound.Params?.Deserialize(AcpJsonContext.Default.SessionNotification);
            if (notification is null || !string.Equals(notification.SessionId, turn.SessionId, StringComparison.Ordinal))
            {
                return;
            }

            if (notification.Update.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("session/update carries no update object.");
            }

            if (AcpWireMapping.MapSessionUpdate(notification.Update) is { } harnessEvent)
            {
                turn.Events.Writer.TryWrite(harnessEvent);
            }
        }
        catch (JsonException)
        {
            turn.Events.Writer.TryWrite(new ErrorEvent("omp acp sent a session/update that could not be decoded.", Retryable: false));
        }
    }

    private async Task HandleAgentRequestAsync(long id, AcpFrames.Inbound request)
    {
        if (string.Equals(request.Method, PermissionMethod, StringComparison.Ordinal))
        {
            await AnswerPermissionAsync(id, request).ConfigureAwait(false);
            return;
        }

        // The client advertises no fs or terminal capabilities, so the agent must not call those methods; anything else is unknown.
        var error = new AcpFrames.AcpError { Code = MethodNotFound, Message = $"Method not found: {request.Method}" };
        await SendFrameAsync(new AcpFrames.ErrorResponse(id, error), AcpJsonContext.Default.ErrorResponse, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task AnswerPermissionAsync(long id, AcpFrames.Inbound request)
    {
        var raw = request.Params;
        AcpFrames.PermissionRequestParams? parameters = null;
        try
        {
            parameters = raw?.Deserialize(AcpJsonContext.Default.PermissionRequestParams);
        }
        catch (JsonException)
        {
            parameters = null;
        }

        if (parameters?.ToolCall is not { } toolCall || raw is null)
        {
            var invalid = new AcpFrames.AcpError { Code = InvalidParams, Message = "session/request_permission requires params.toolCall." };
            await SendFrameAsync(new AcpFrames.ErrorResponse(id, invalid), AcpJsonContext.Default.ErrorResponse, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        var permission = new AcpPermissionRequest(
            toolCall.ToolCallId,
            toolCall.Title ?? toolCall.Kind ?? "tool",
            toolCall.Kind ?? string.Empty,
            raw.Value.GetRawText());
        var granted = _options.ApprovalPolicy.Allows(permission);
        ActiveTurn()?.Events.Writer.TryWrite(new PermissionRequestEvent(permission.CallId, permission.Name, permission.PayloadJson));

        var option = SelectOption(parameters.Options, granted);
        var outcome = option is null
            ? new AcpFrames.PermissionOutcome("cancelled", null)
            : new AcpFrames.PermissionOutcome("selected", option.OptionId);
        await SendFrameAsync(
            new AcpFrames.Response<AcpFrames.PermissionResponse>(id, new AcpFrames.PermissionResponse(outcome)),
            AcpJsonContext.Default.ResponsePermissionResponse,
            CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Picks the option that expresses the decision. A grant selects an allow option; a denial selects a reject option.
    /// When the agent offered no matching option, the request is answered as cancelled, which agents treat as a denial.
    /// </summary>
    private static AcpFrames.PermissionOption? SelectOption(IReadOnlyList<AcpFrames.PermissionOption>? options, bool granted)
    {
        if (options is null)
        {
            return null;
        }

        foreach (var kind in granted ? AllowKinds : RejectKinds)
        {
            var option = options.FirstOrDefault(candidate => string.Equals(candidate.Kind, kind, StringComparison.Ordinal));
            if (option is not null)
            {
                return option;
            }
        }

        return null;
    }

    private PromptTurn BeginTurn()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_sessionClosed)
            {
                throw new InvalidOperationException("The ACP session is closed.");
            }

            _machine.TransitionTo(HarnessState.Streaming);
            var turn = new PromptTurn(SessionId ?? throw new InvalidOperationException("The ACP session has no sessionId."));
            _turn = turn;
            return turn;
        }
    }

    private async Task StartPromptAsync(PromptTurn turn, string prompt, CancellationToken cancellationToken)
    {
        var sent = false;
        try
        {
            long id;
            lock (_gate)
            {
                ThrowIfConnectionLost();
                id = ++_lastRequestId;
                turn.RequestId = id;
                _pending[id] = new PendingCall(frame => OnPromptResponse(turn, frame), error => FailTurn(turn, error));
            }

            var parameters = new AcpFrames.PromptParams(turn.SessionId, [new AcpFrames.ContentBlock { Text = prompt }]);
            await SendFrameAsync(
                new AcpFrames.Request<AcpFrames.PromptParams>(id, "session/prompt", parameters),
                AcpJsonContext.Default.RequestPromptParams,
                cancellationToken).ConfigureAwait(false);
            sent = true;
        }
        finally
        {
            if (!sent)
            {
                // A cancelled start wrote nothing and leaves the session usable; any other failure leaves the wire state unknown.
                RollBackStart(turn, faultSession: !cancellationToken.IsCancellationRequested);
            }
        }
    }

    private void RollBackStart(PromptTurn turn, bool faultSession)
    {
        lock (_gate)
        {
            _pending.Remove(turn.RequestId);
            turn.Settled = true;
            if (_turn == turn)
            {
                _turn = null;
            }

            if (faultSession)
            {
                TryFaultLocked();
            }
            else if (!_disposed && !_connectionLost && _machine.State is HarnessState.Streaming)
            {
                _machine.TransitionTo(HarnessState.Ready);
            }
        }
    }

    /// <summary>Runs on the pump. Completes the turn: an error becomes <see cref="ErrorEvent"/> then <c>CompletedEvent("error")</c>; a result becomes its stop reason.</summary>
    private void OnPromptResponse(PromptTurn turn, AcpFrames.Inbound response)
    {
        var writer = turn.Events.Writer;
        if (response.Error is { } error)
        {
            Settle(turn);
            writer.TryWrite(new ErrorEvent(error.Message, Retryable: false));
            writer.TryWrite(new CompletedEvent("error"));
            writer.TryComplete();
            return;
        }

        // Decode before settling: a malformed result fails the turn through connection loss instead of ending it quietly.
        var stopReason = ReadResult(response, AcpJsonContext.Default.PromptResult, "session/prompt").StopReason;
        if (stopReason.Length == 0)
        {
            throw new AcpRequestFailedHarnessException("omp acp answered 'session/prompt' without a stopReason.", "session/prompt");
        }

        Settle(turn);
        writer.TryWrite(new CompletedEvent(stopReason));
        writer.TryComplete();
    }

    private void Settle(PromptTurn turn)
    {
        lock (_gate)
        {
            turn.Settled = true;
            if (_turn == turn)
            {
                _turn = null;
            }

            if (!_disposed && !_connectionLost && _machine.State is HarnessState.Streaming or HarnessState.Cancelling)
            {
                _machine.TransitionTo(HarnessState.Ready);
            }
        }
    }

    private void FailTurn(PromptTurn turn, Exception error)
    {
        lock (_gate)
        {
            if (turn.Settled)
            {
                return;
            }

            turn.Settled = true;
            if (_turn == turn)
            {
                _turn = null;
            }
        }

        turn.Events.Writer.TryComplete(error);
    }

    private async Task AbandonTurnAsync(PromptTurn turn)
    {
        var cancel = false;
        lock (_gate)
        {
            if (turn.Settled)
            {
                return;
            }

            cancel = _machine.State == HarnessState.Streaming;
            if (cancel)
            {
                _machine.TransitionTo(HarnessState.Cancelling);
            }
        }

        if (!cancel)
        {
            return;
        }

        try
        {
            await SendCancelAsync(turn, CancellationToken.None).ConfigureAwait(false);
        }
        catch (HarnessException)
        {
            // The connection is already gone; the pump fails the turn and the session.
        }
    }

    private async Task SendCancelAsync(PromptTurn turn, CancellationToken cancellationToken)
    {
        await SendFrameAsync(
            new AcpFrames.Notification<AcpFrames.CancelParams>("session/cancel", new AcpFrames.CancelParams(turn.SessionId)),
            AcpJsonContext.Default.NotificationCancelParams,
            cancellationToken).ConfigureAwait(false);
        await SendFrameAsync(
            new AcpFrames.Notification<AcpFrames.CancelRequestParams>("$/cancel_request", new AcpFrames.CancelRequestParams(turn.RequestId)),
            AcpJsonContext.Default.NotificationCancelRequestParams,
            cancellationToken).ConfigureAwait(false);
    }

    private void OnConnectionLost(Exception failure)
    {
        List<PendingCall> pending;
        PromptTurn? turn;
        lock (_gate)
        {
            _connectionLost = true;
            TryFaultLocked();
            pending = [.. _pending.Values];
            _pending.Clear();
            turn = _turn;
        }

        foreach (var call in pending)
        {
            call.OnFailure(failure);
        }

        if (turn is not null)
        {
            FailTurn(turn, failure);
        }
    }

    private async Task FailOpenAsync()
    {
        lock (_gate)
        {
            TryFaultLocked();
        }

        await _transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private void TryFaultLocked()
    {
        if (!_disposed && HarnessStateMachine.IsAllowed(_machine.State, HarnessState.Faulted))
        {
            _machine.TransitionTo(HarnessState.Faulted);
        }
    }

    private PromptTurn? ActiveTurn()
    {
        lock (_gate)
        {
            return _turn;
        }
    }

    private void SetSessionId(string sessionId)
    {
        lock (_gate)
        {
            SessionId = sessionId;
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private void ThrowIfConnectionLost()
    {
        if (_connectionLost)
        {
            throw new DisconnectedHarnessException("The omp acp connection was lost.", SessionId);
        }
    }

    private static AcpFrames.Inbound ParseFrame(ReadOnlyMemory<byte> frame)
    {
        try
        {
            return JsonSerializer.Deserialize(frame.Span, AcpJsonContext.Default.Inbound)
                ?? throw AcpFrameDiagnostics.Malformed(frame, "empty envelope");
        }
        catch (JsonException ex)
        {
            throw AcpFrameDiagnostics.Malformed(frame, "invalid envelope", ex);
        }
    }

    private static bool TryGetId(JsonElement? id, out long value)
    {
        if (id is { ValueKind: JsonValueKind.Number } element && element.TryGetInt64(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static T ReadResult<T>(AcpFrames.Inbound response, JsonTypeInfo<T> typeInfo, string method)
        where T : class
    {
        if (response.Result is not { } result)
        {
            throw new AcpRequestFailedHarnessException($"omp acp answered '{method}' without a result.", method);
        }

        try
        {
            return result.Deserialize(typeInfo)
                ?? throw new AcpRequestFailedHarnessException($"omp acp answered '{method}' with a null result.", method);
        }
        catch (JsonException ex)
        {
            throw new AcpRequestFailedHarnessException($"omp acp answered '{method}' with an unreadable result.", method, ex);
        }
    }

    /// <summary>A request awaiting its response. Callbacks run on the pump.</summary>
    private sealed record PendingCall(Action<AcpFrames.Inbound> OnResponse, Action<Exception> OnFailure);

    /// <summary>One <c>session/prompt</c> in flight, and the channel its events stream through.</summary>
    private sealed class PromptTurn(string sessionId)
    {
        public string SessionId { get; } = sessionId;

        public Channel<HarnessEvent> Events { get; } = Channel.CreateUnbounded<HarnessEvent>(new UnboundedChannelOptions { SingleReader = true });

        public long RequestId { get; set; }

        /// <summary>Gets or sets a value indicating whether the turn has ended, normally or not. Guarded by the session gate.</summary>
        public bool Settled { get; set; }
    }
}

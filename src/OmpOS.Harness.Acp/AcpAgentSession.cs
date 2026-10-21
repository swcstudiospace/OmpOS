using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
/// change happens at a known point in that order. A request the policy admits waits for its decision on a task of its own,
/// so the pump never blocks on it.
/// </summary>
public sealed class AcpAgentSession : IOmpSession
{
    private const string UpdateMethod = "session/update";
    private const string PermissionMethod = "session/request_permission";
    private const string AuthenticateMethod = "authenticate";
    private const string FsReadMethod = "fs/read_text_file";
    private const string FsWriteMethod = "fs/write_text_file";
    private const int MethodNotFound = -32601;
    private const int InvalidParams = -32602;
    private const int InternalError = -32603;

    private static readonly string[] AllowKinds = ["allow_once", "allow_always"];
    private static readonly string[] RejectKinds = ["reject_once", "reject_always"];
    internal static readonly TimeSpan DefaultPermissionDecisionTimeout = TimeSpan.FromSeconds(30);
    private readonly IAcpTransport _transport;
    private readonly AcpClientOptions _options;
    private readonly string? _fsRoot;
    private readonly IPermissionResolver? _permissionResolver;
    private readonly TimeSpan _permissionDecisionTimeout;
    private readonly HarnessStateMachine _machine = new();
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<long, PendingCall> _pending = [];
    private long _lastRequestId;
    private PromptTurn? _turn;
    private Task? _pumpTask;
    private bool _connectionLost;
    private bool _sessionClosed;
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="AcpAgentSession"/> class. With no resolver, a request the policy admits is denied.</summary>
    /// <param name="transport">The transport to the agent. The session opens it; the session closes it.</param>
    /// <param name="options">Session options.</param>
    public AcpAgentSession(IAcpTransport transport, AcpClientOptions options)
        : this(transport, options, null, DefaultPermissionDecisionTimeout)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AcpAgentSession"/> class.</summary>
    /// <param name="transport">The transport to the agent. The session opens it; the session closes it.</param>
    /// <param name="options">Session options.</param>
    /// <param name="permissionResolver">
    /// Supplies the decision for a request the approval policy admits. Null means no decision can arrive, so such a request is denied.
    /// </param>
    /// <param name="permissionDecisionTimeout">How long a parked request waits for its decision before it is denied.</param>
    public AcpAgentSession(IAcpTransport transport, AcpClientOptions options, IPermissionResolver? permissionResolver, TimeSpan permissionDecisionTimeout)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);
        if (permissionDecisionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(permissionDecisionTimeout), permissionDecisionTimeout, "The permission decision timeout must be positive.");
        }

        _transport = transport;
        _options = options;
        _fsRoot = options.FsRoot is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.FsRoot));
        _permissionResolver = permissionResolver;
        _permissionDecisionTimeout = permissionDecisionTimeout;
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
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "MED-01: observing the pump must not throw its recorded failure out of disposal.")]
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
            catch (Exception)
            {
                // The pump records connection loss itself; disposal must not throw.
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

        if (options.FsRoot is not null && !Path.IsPathFullyQualified(options.FsRoot))
        {
            throw new ArgumentException("FsRoot must be an absolute directory path.", nameof(options));
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
        var fsEnabled = _fsRoot is not null;
        var parameters = new AcpFrames.InitializeParams(
            AcpFrames.ProtocolVersion,
            new AcpFrames.ClientCapabilities(new AcpFrames.FsCapabilities(ReadTextFile: fsEnabled, WriteTextFile: fsEnabled)));
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

        if (result.AuthMethods is { Count: > 0 } authMethods)
        {
            await AuthenticateAsync(authMethods, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sends <c>authenticate</c> with the configured <see cref="AcpClientOptions.AuthMethodId"/> before any session
    /// is opened. The method id is never guessed: an unconfigured or unknown id fails the handshake.
    /// </summary>
    /// <param name="authMethods">The authentication methods the agent advertised.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="AcpRequestFailedHarnessException">
    /// No method id is configured, the configured id is unknown, or the agent answers with an error.
    /// </exception>
    private async Task AuthenticateAsync(IReadOnlyList<AcpFrames.AuthMethod> authMethods, CancellationToken cancellationToken)
    {
        var methodId = _options.AuthMethodId;
        if (string.IsNullOrWhiteSpace(methodId))
        {
            throw new AcpRequestFailedHarnessException(
                "omp acp requires authentication but no AuthMethodId is configured on AcpClientOptions.",
                AuthenticateMethod);
        }

        var known = false;
        foreach (var method in authMethods)
        {
            if (string.Equals(method.Id, methodId, StringComparison.Ordinal))
            {
                known = true;
                break;
            }
        }

        if (!known)
        {
            throw new AcpRequestFailedHarnessException(
                $"The configured AuthMethodId '{methodId}' is not one of the methods omp acp advertised.",
                AuthenticateMethod);
        }

        await CallAsync(
            AuthenticateMethod,
            new AcpFrames.AuthenticateParams(methodId),
            AcpJsonContext.Default.RequestAuthenticateParams,
            _options.HandshakeTimeout,
            cancellationToken).ConfigureAwait(false);
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

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "MED-01: the pump invokes untrusted hook and transport code; any failure must fault the session with the real cause preserved.")]
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
        catch (Exception ex)
        {
            // The pump invokes untrusted code (the approval hook, the transport). A non-harness failure
            // still ends the stream, but the real exception is kept as the cause instead of a generic close.
            failure = ex is HarnessException hex ? hex
                : new DisconnectedHarnessException("The omp acp session failed.", SessionId, ex);
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
            // String ids are legal JSON-RPC 2.0 but this client correlates its own requests numerically:
            // answer with an error instead of faulting the session. Any other id shape is malformed.
            if (inbound.Id is { ValueKind: JsonValueKind.String } element && element.GetString() is { } rawId)
            {
                await AnswerStringIdRequestAsync(rawId, inbound).ConfigureAwait(false);
                return;
            }

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
                WriteTurnEvent(turn, harnessEvent);
            }
        }
        catch (JsonException)
        {
            WriteTurnEvent(turn, new DiagnosticEvent("omp acp sent a session/update that could not be decoded."));
        }
    }

    private async Task HandleAgentRequestAsync(long id, AcpFrames.Inbound request)
    {
        if (string.Equals(request.Method, PermissionMethod, StringComparison.Ordinal))
        {
            await AnswerPermissionAsync(id, request).ConfigureAwait(false);
            return;
        }

        if (string.Equals(request.Method, FsReadMethod, StringComparison.Ordinal))
        {
            await AnswerFsReadAsync(id, request).ConfigureAwait(false);
            return;
        }

        if (string.Equals(request.Method, FsWriteMethod, StringComparison.Ordinal))
        {
            await AnswerFsWriteAsync(id, request).ConfigureAwait(false);
            return;
        }

        // The client advertises no terminal capabilities, so the agent must not call those methods; anything else is unknown.
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
        var turn = ActiveTurn();

        // The policy ceiling is consulted first: a request it forbids is denied at once and never waits for a decision.
        var granted = IsGrantedByPolicy(permission, turn);
        var requestEvent = new PermissionRequestEvent(permission.CallId, permission.Name, permission.PayloadJson, id.ToString(CultureInfo.InvariantCulture));
        if (turn is not null)
        {
            WriteTurnEvent(turn, requestEvent);
        }

        if (granted && _permissionResolver is { } resolver)
        {
            // The policy admits the request and a decision can arrive: park it under its id and keep the pump reading.
            _ = DecideParkedPermissionAsync(id, parameters.Options, requestEvent, resolver, turn);
            return;
        }

        // Reached when the policy forbids the request, or admits it with no resolver to decide it. Neither path grants.
        await SendPermissionOutcomeAsync(id, parameters.Options, granted: false, CancellationToken.None).ConfigureAwait(false);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "MED-01: the narrowing hook is untrusted caller code; a hook failure denies closed with a diagnostic.")]
    private bool IsGrantedByPolicy(AcpPermissionRequest permission, PromptTurn? turn)
    {
        try
        {
            return _options.ApprovalPolicy.Allows(permission);
        }
        catch (Exception ex)
        {
            // The narrowing hook is untrusted caller code: fail closed (deny) and record the
            // hook failure as a diagnostic event instead of faulting the pump with the raw exception.
            if (turn is not null)
            {
                WriteTurnEvent(turn, new DiagnosticEvent($"Approval policy failed: {ex.GetType().Name}. Denied."));
            }

            return false;
        }
    }

    /// <summary>
    /// Answers a parked permission request once its decision arrives. Runs off the pump. A timeout, a cancelled wait
    /// or a failing resolver denies the request; a failure to write the answer faults the session, as a dropped frame does.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "MED-01: the resolver is untrusted caller code and the transport is fallible; each failure denies closed or faults the session with its cause.")]
    private async Task DecideParkedPermissionAsync(
        long id,
        IReadOnlyList<AcpFrames.PermissionOption>? options,
        PermissionRequestEvent requestEvent,
        IPermissionResolver resolver,
        PromptTurn? turn)
    {
        var granted = false;
        try
        {
            using var timeout = new CancellationTokenSource(_permissionDecisionTimeout);
            var decision = await resolver.ResolveAsync(requestEvent, timeout.Token).ConfigureAwait(false);
            granted = decision == PermissionDecision.Allow;
        }
        catch (OperationCanceledException)
        {
            // A timeout or a cancelled turn is the denial: granted stays false.
        }
        catch (Exception ex)
        {
            RecordParkedDiagnostic(turn, $"Permission decision failed: {ex.GetType().Name}. Denied.");
        }

        try
        {
            await SendPermissionOutcomeAsync(id, options, granted, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            FaultAfterLostAnswer(turn, ex);
        }
    }

    /// <summary>Records a diagnostic on a parked request's turn while the turn is still open; a settled turn's stream is complete.</summary>
    private void RecordParkedDiagnostic(PromptTurn? turn, string message)
    {
        if (turn is null)
        {
            return;
        }

        lock (_gate)
        {
            if (!turn.Settled)
            {
                WriteTurnEvent(turn, new DiagnosticEvent(message));
            }
        }
    }

    private Task SendPermissionOutcomeAsync(long id, IReadOnlyList<AcpFrames.PermissionOption>? options, bool granted, CancellationToken cancellationToken)
    {
        var option = SelectOption(options, granted);
        var outcome = option is null
            ? new AcpFrames.PermissionOutcome("cancelled", null)
            : new AcpFrames.PermissionOutcome("selected", option.OptionId);
        return SendFrameAsync(
            new AcpFrames.Response<AcpFrames.PermissionResponse>(id, new AcpFrames.PermissionResponse(outcome)),
            AcpJsonContext.Default.ResponsePermissionResponse,
            cancellationToken);
    }

    /// <summary>Fails the turn and faults the session when an answer to the agent could not be written.</summary>
    private void FaultAfterLostAnswer(PromptTurn? turn, Exception error)
    {
        if (turn is not null)
        {
            FailTurn(turn, new DisconnectedHarnessException("omp acp could not receive the answer to a permission request.", SessionId, error));
        }

        lock (_gate)
        {
            TryFaultLocked();
        }
    }

    /// <summary>
    /// Answers an agent-initiated request that carries a string identifier. The client correlates its own
    /// requests numerically, so the request cannot be processed: it is answered with an error instead of
    /// faulting the session. The raw id text is echoed verbatim, as JSON-RPC 2.0 requires.
    /// </summary>
    private async Task AnswerStringIdRequestAsync(string rawId, AcpFrames.Inbound request)
    {
        var error = new AcpFrames.AcpError { Code = InvalidParams, Message = $"Agent requests with string ids are not supported: '{request.Method}'." };
        await SendFrameAsync(new AcpFrames.StringErrorResponse(rawId, error), AcpJsonContext.Default.StringErrorResponse, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Answers an agent-initiated <c>fs/read_text_file</c> request. Without <c>FsRoot</c> the capability is
    /// disabled; with it, the requested path must stay under the root, including through symlinks.
    /// </summary>
    private async Task AnswerFsReadAsync(long id, AcpFrames.Inbound request)
    {
        var parameters = DeserializeFsParams(request.Params, AcpJsonContext.Default.FsReadParams);
        if (parameters is null || string.IsNullOrEmpty(parameters.Path))
        {
            await SendFsErrorAsync(id, InvalidParams, $"{FsReadMethod} requires params.path.").ConfigureAwait(false);
            return;
        }

        if (parameters.Line is < 1 || parameters.Limit is < 1)
        {
            await SendFsErrorAsync(id, InvalidParams, $"{FsReadMethod} requires a 1-based line and a positive limit.").ConfigureAwait(false);
            return;
        }

        if (CheckFsAccess(FsReadMethod, parameters.Path, out var fullPath) is { } denial)
        {
            await SendFsErrorAsync(id, denial.Code, denial.Message).ConfigureAwait(false);
            return;
        }

        var lines = await TryReadFsLinesAsync(id, fullPath).ConfigureAwait(false);
        if (lines is null)
        {
            return;
        }

        var start = (parameters.Line ?? 1) - 1;
        var selected = lines.Skip(start);
        if (parameters.Limit.HasValue)
        {
            selected = selected.Take(parameters.Limit.Value);
        }

        await SendFrameAsync(
            new AcpFrames.Response<AcpFrames.FsReadResult>(id, new AcpFrames.FsReadResult(string.Join("\n", selected))),
            AcpJsonContext.Default.ResponseFsReadResult,
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<string[]?> TryReadFsLinesAsync(long id, string fullPath)
    {
        try
        {
            if (Directory.Exists(fullPath) || !File.Exists(fullPath))
            {
                await SendFsErrorAsync(id, InvalidParams, $"{FsReadMethod} found no file at the requested path.").ConfigureAwait(false);
                return null;
            }

            return await File.ReadAllLinesAsync(fullPath, CancellationToken.None).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            await SendFsErrorAsync(id, InternalError, $"{FsReadMethod} could not read the file: {ex.Message}").ConfigureAwait(false);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            await SendFsErrorAsync(id, InternalError, $"{FsReadMethod} could not read the file: {ex.Message}").ConfigureAwait(false);
            return null;
        }
    }

    /// <summary>
    /// Answers an agent-initiated <c>fs/write_text_file</c> request. Without <c>FsRoot</c> the capability is
    /// disabled; with it, the requested path must stay under the root, including through symlinks. Parent
    /// directories are created when they do not exist yet.
    /// </summary>
    private async Task AnswerFsWriteAsync(long id, AcpFrames.Inbound request)
    {
        var parameters = DeserializeFsParams(request.Params, AcpJsonContext.Default.FsWriteParams);
        if (parameters is null || string.IsNullOrEmpty(parameters.Path))
        {
            await SendFsErrorAsync(id, InvalidParams, $"{FsWriteMethod} requires params.path.").ConfigureAwait(false);
            return;
        }

        if (parameters.Content is null)
        {
            await SendFsErrorAsync(id, InvalidParams, $"{FsWriteMethod} requires params.content.").ConfigureAwait(false);
            return;
        }

        if (CheckFsAccess(FsWriteMethod, parameters.Path, out var fullPath) is { } denial)
        {
            await SendFsErrorAsync(id, denial.Code, denial.Message).ConfigureAwait(false);
            return;
        }

        if (!await TryWriteFsFileAsync(id, fullPath, parameters.Content).ConfigureAwait(false))
        {
            return;
        }

        await SendFrameAsync(
            new AcpFrames.Response<AcpFrames.FsWriteResult>(id, new AcpFrames.FsWriteResult()),
            AcpJsonContext.Default.ResponseFsWriteResult,
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<bool> TryWriteFsFileAsync(long id, string fullPath, string content)
    {
        try
        {
            if (Directory.Exists(fullPath))
            {
                await SendFsErrorAsync(id, InvalidParams, $"{FsWriteMethod} cannot overwrite a directory.").ConfigureAwait(false);
                return false;
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, content, CancellationToken.None).ConfigureAwait(false);
            return true;
        }
        catch (IOException ex)
        {
            await SendFsErrorAsync(id, InternalError, $"{FsWriteMethod} could not write the file: {ex.Message}").ConfigureAwait(false);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            await SendFsErrorAsync(id, InternalError, $"{FsWriteMethod} could not write the file: {ex.Message}").ConfigureAwait(false);
            return false;
        }
    }

    private static T? DeserializeFsParams<T>(JsonElement? raw, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        try
        {
            return raw?.Deserialize(typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Denies an fs call when the capability is disabled or the path leaves the root. Returns null when the call may proceed.
    /// </summary>
    private AcpFrames.AcpError? CheckFsAccess(string method, string requestedPath, out string fullPath)
    {
        if (_fsRoot is null)
        {
            fullPath = string.Empty;
            return new AcpFrames.AcpError { Code = MethodNotFound, Message = $"{method} is disabled: no FsRoot is configured." };
        }

        if (!TryResolveInsideRoot(_fsRoot, requestedPath, method, out fullPath, out var violation))
        {
            return new AcpFrames.AcpError { Code = InvalidParams, Message = violation };
        }

        return null;
    }

    private Task SendFsErrorAsync(long id, long code, string message)
    {
        var error = new AcpFrames.AcpError { Code = code, Message = message };
        return SendFrameAsync(new AcpFrames.ErrorResponse(id, error), AcpJsonContext.Default.ErrorResponse, CancellationToken.None);
    }

    /// <summary>
    /// Resolves a requested fs path against the root, rejecting traversal outside the root, the root itself,
    /// and symlink escapes. Only existing components can be links, so missing parents are safe to create.
    /// </summary>
    private static bool TryResolveInsideRoot(string rootFull, string requestedPath, string method, out string fullPath, out string violation)
    {
        string combined;
        try
        {
            combined = Path.GetFullPath(Path.Combine(rootFull, requestedPath));
        }
        catch (ArgumentException)
        {
            fullPath = string.Empty;
            violation = $"{method} was given a path that cannot be resolved.";
            return false;
        }
        catch (NotSupportedException)
        {
            fullPath = string.Empty;
            violation = $"{method} was given a path that cannot be resolved.";
            return false;
        }

        if (string.Equals(combined, rootFull, RootComparison))
        {
            fullPath = string.Empty;
            violation = $"{method} requires a file inside the allowed root.";
            return false;
        }

        if (!IsWithinRoot(rootFull, combined) || HasSymlinkEscape(rootFull, combined))
        {
            fullPath = string.Empty;
            violation = $"{method} resolved outside the allowed root.";
            return false;
        }

        fullPath = combined;
        violation = string.Empty;
        return true;
    }

    private static bool IsWithinRoot(string rootFull, string candidate)
    {
        if (candidate.Length < rootFull.Length || !candidate.StartsWith(rootFull, RootComparison))
        {
            return false;
        }

        // A filesystem root such as "/" already ends with a separator, so the character after it is the child's first.
        return candidate.Length == rootFull.Length
            || Path.EndsInDirectorySeparator(rootFull)
            || candidate[rootFull.Length] == Path.DirectorySeparatorChar;
    }

    private static StringComparison RootComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Checks the resolved path and every existing ancestor down to the root for links whose final target
    /// leaves the root. Ancestors that do not exist yet cannot be links.
    /// </summary>
    private static bool HasSymlinkEscape(string rootFull, string fullPath)
    {
        var current = fullPath;
        while (current is not null
            && !string.Equals(current, rootFull, RootComparison)
            && IsWithinRoot(rootFull, current))
        {
            FileSystemInfo info = Directory.Exists(current) ? (FileSystemInfo)new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists)
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null && !IsWithinRoot(rootFull, Path.GetFullPath(target.FullName)))
                {
                    return true;
                }
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
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

    /// <summary>Runs on the pump. Completes the turn: an error ends it with an <see cref="ErrorEvent"/> alone; a result ends it with a <see cref="CompletedEvent"/> carrying its stop reason.</summary>
    private void OnPromptResponse(PromptTurn turn, AcpFrames.Inbound response)
    {
        if (response.Error is { } error)
        {
            Settle(turn);
            EndTurn(turn, new ErrorEvent(error.Message, Retryable: false));
            return;
        }

        // Decode before settling: a malformed result fails the turn through connection loss instead of ending it quietly.
        var stopReason = ReadResult(response, AcpJsonContext.Default.PromptResult, "session/prompt").StopReason;
        if (stopReason.Length == 0)
        {
            throw new AcpRequestFailedHarnessException("omp acp answered 'session/prompt' without a stopReason.", "session/prompt");
        }

        Settle(turn);
        EndTurn(turn, new CompletedEvent(stopReason));
    }

    /// <summary>
    /// Writes the turn's final events, then completes its stream. A final event the bounded buffer cannot take is lost
    /// to the consumer, so the turn fails with <see cref="BackpressureHarnessException"/> and the session faults, the same
    /// as a dropped update. A clean end is never reported in its place.
    /// </summary>
    private void EndTurn(PromptTurn turn, params HarnessEvent[] finalEvents)
    {
        var writer = turn.Events.Writer;
        foreach (HarnessEvent harnessEvent in finalEvents)
        {
            if (!writer.TryWrite(harnessEvent))
            {
                writer.TryComplete(TurnBufferFull());
                lock (_gate)
                {
                    TryFaultLocked();
                }

                return;
            }
        }

        writer.TryComplete();
    }

    private static BackpressureHarnessException TurnBufferFull() =>
        new("omp acp produced events faster than the consumer read them.", "acp");

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

    /// <summary>
    /// Writes an event into the active turn. When the bounded turn buffer is full the consumer has fallen
    /// behind: the turn fails with <see cref="BackpressureHarnessException"/> and the session faults, because
    /// the wire state after a dropped update is unknown.
    /// </summary>
    private void WriteTurnEvent(PromptTurn turn, HarnessEvent harnessEvent)
    {
        if (!turn.Events.Writer.TryWrite(harnessEvent))
        {
            FailTurn(turn, TurnBufferFull());
            lock (_gate)
            {
                TryFaultLocked();
            }
        }
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

    /// <summary>The turn event buffer capacity, matching the RPC prompt slot: a flood beyond it faults the session.</summary>
    private const int TurnCapacity = 4096;

    /// <summary>One <c>session/prompt</c> in flight, and the channel its events stream through.</summary>
    private sealed class PromptTurn(string sessionId)
    {
        public string SessionId { get; } = sessionId;

        public Channel<HarnessEvent> Events { get; } = Channel.CreateBounded<HarnessEvent>(
            new BoundedChannelOptions(TurnCapacity) { SingleReader = true });

        public long RequestId { get; set; }

        /// <summary>Gets or sets a value indicating whether the turn has ended, normally or not. Guarded by the session gate.</summary>
        public bool Settled { get; set; }
    }
}

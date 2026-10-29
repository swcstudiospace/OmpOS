using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp.AcpProtocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// An omp conversation over ACP v1. One pump task owns every inbound frame: it correlates responses by id, routes
/// <c>session/update</c> notifications into the active prompt stream, and answers agent permission requests through
/// <see cref="AcpApprovalPolicy"/>. Because the pump is the only reader, events arrive in wire order and every state
/// change happens at a known point in that order. A request the policy admits waits for its decision on a task of its own,
/// so the pump never blocks on it.
/// </summary>
public sealed partial class AcpAgentSession : IOmpSession, IOmpSessionIdentity, IOmpModelControl, IOmpSessionOptions, IOmpSessionCommands, IOmpLiveSteering, IOmpSessionFork
{
    private const string UpdateMethod = "session/update";
    private const string SetConfigOptionMethod = "session/set_config_option";
    private const string ConfigOptionUpdateKind = "config_option_update";
    private const string AvailableCommandsUpdateKind = "available_commands_update";
    private const string ModelOptionId = "model";
    private const string PermissionMethod = "session/request_permission";
    private const string AuthenticateMethod = "authenticate";
    private const string FsReadMethod = "fs/read_text_file";
    private const string FsWriteMethod = "fs/write_text_file";
    private const int MethodNotFound = -32601;
    private const int InvalidParams = -32602;
    private const int InternalError = -32603;
    private const string SteeringMethod = "_session/steering";
    private const string FollowUpQueueName = "followUp";
    private const string SteeringQueueName = "steering";

    /// <summary>The longest steering message, in UTF-8 bytes: the limit of the steering inbox, so a message fits either mechanism.</summary>
    public const int MaxSteerBytes = 8192;

    /// <summary>How long disposal waits for the agent to answer <c>session/close</c> before it stops the child anyway: the close is best-effort, and an agent that never answers it must not keep its child alive for a whole request timeout.</summary>
    private static readonly TimeSpan DisposeCloseTimeout = TimeSpan.FromSeconds(3);

    /// <summary>How long a <c>sessionEnd</c> hook may delay the close of a session.</summary>
    private static readonly TimeSpan HookSessionEndTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The largest file whose previous text is read to describe an <c>afterFileEdit</c>; a larger file is reported without a previous text.</summary>
    private const long MaxHookCompareBytes = 2L * 1024 * 1024;

    private static readonly Action<ILogger, string?, Exception?> PumpFailed =
        LoggerMessage.Define<string?>(
            LogLevel.Error,
            new EventId(1, "AcpPumpFailed"),
            "The omp acp pump failed for session {SessionId}.");

    private static readonly Action<ILogger, string, string, Exception?> HookGateFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2, "AcpHookGateFailed"),
            "The hook gate failed at {Point} with {ExceptionType}; the action was allowed.");

    private static readonly string[] AllowKinds = ["allow_once", "allow_always"];
    private static readonly string[] RejectKinds = ["reject_once", "reject_always"];
    internal static readonly TimeSpan DefaultPermissionDecisionTimeout = PermissionDecisionWindow.Default;
    private readonly IAcpTransport _transport;
    private readonly AcpClientOptions _options;
    private readonly string? _fsRoot;
    private readonly AcpFrames.McpServerFrame[] _mcpServers;
    private readonly IPermissionResolver? _permissionResolver;
    private readonly TimeSpan _permissionDecisionTimeout;
    private readonly HarnessStateMachine _machine = new();
    private readonly System.Threading.Lock _gate = new();
    private readonly ILogger<AcpAgentSession> _logger;
    private readonly IAcpHookGate? _hookGate;
    private readonly AcpHookSignals? _hookSignals;
    private int _endNotified;
    private readonly Dictionary<long, PendingCall> _pending = [];
    private long _lastRequestId;
    private PromptTurn? _turn;
    private Task? _pumpTask;
    private bool _connectionLost;
    private bool _sessionClosed;
    private IReadOnlyList<AcpConfigOption> _configOptions = [];
    private IReadOnlyList<OmpModelChoice> _modelChoices = [];
    private string? _modelOptionId;
    private string? _currentModelId;
    private bool _optionChanging;
    private bool _steeringSupported;
    private bool? _supportsImages;
    private bool _openFailed;
    private OmpSessionOption[] _sessionOptions = [];
    private IReadOnlyList<AcpAvailableCommand> _availableCommands = [];
    private OmpCommand[] _commands = [];
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
    /// <param name="logger">Receives pump faults at Error. Null discards them.</param>
    public AcpAgentSession(IAcpTransport transport, AcpClientOptions options, IPermissionResolver? permissionResolver, TimeSpan permissionDecisionTimeout, ILogger<AcpAgentSession>? logger = null)
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
        _mcpServers = AcpWireMapping.ToMcpFrames(options.McpServers);
        _permissionResolver = permissionResolver;
        _permissionDecisionTimeout = permissionDecisionTimeout;
        _logger = logger ?? NullLogger<AcpAgentSession>.Instance;
        _hookGate = options.HookGate;
        _hookSignals = options.HookGate is null ? null : new AcpHookSignals(options.HookGate, TimeProvider.System);
    }

    /// <summary>Gets the ACP session identifier once the session has been created or resumed.</summary>
    public string? SessionId { get; private set; }

    /// <summary>Gets the ACP session identifier once the session has been created or resumed. It is the same value as <see cref="SessionId"/>.</summary>
    public string? NativeSessionId => SessionId;

    /// <summary>
    /// Gets the select-style configuration options the agent reported in <c>session/new</c>, <c>session/load</c>, a
    /// <c>session/set_config_option</c> reply or a <c>config_option_update</c> notification, last report first. Empty until the session is open.
    /// </summary>
    public IReadOnlyList<AcpConfigOption> ConfigOptions
    {
        get
        {
            lock (_gate)
            {
                return _configOptions;
            }
        }
    }

    /// <inheritdoc />
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
    public event EventHandler? OptionsChanged;

    /// <inheritdoc />
    public IReadOnlyList<OmpSessionOption> Options
    {
        get
        {
            lock (_gate)
            {
                return _sessionOptions;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? CommandsChanged;

    /// <summary>
    /// Gets the commands the agent advertised in its latest <c>available_commands_update</c> notification, in the agent's order. Each notification
    /// replaces the list as a whole. Empty until the session is open and the agent has advertised its commands.
    /// </summary>
    public IReadOnlyList<AcpAvailableCommand> AvailableCommands
    {
        get
        {
            lock (_gate)
            {
                return _availableCommands;
            }
        }
    }

    /// <inheritdoc />
    IReadOnlyList<OmpCommand> IOmpSessionCommands.Commands
    {
        get
        {
            lock (_gate)
            {
                return _commands;
            }
        }
    }

    /// <summary>Gets a value indicating whether omp advertised <c>_meta.steering.supported</c> in its <c>initialize</c> result, so <see cref="SteerAsync"/> can reach a running turn.</summary>
    public bool IsSteeringSupported
    {
        get
        {
            lock (_gate)
            {
                return _steeringSupported;
            }
        }
    }

    /// <inheritdoc />
    bool IOmpLiveSteering.IsSupported => IsSteeringSupported;

    /// <summary>Gets the agent identity reported by <c>initialize</c>.</summary>
    public AcpFrames.AgentInfo? AgentInfo { get; private set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> AdvertisedCapabilities { get; private set; } = FrozenDictionary<string, string>.Empty;

    /// <summary>Gets whether omp advertised <c>agentCapabilities.promptCapabilities.image</c> in its <c>initialize</c> result: true or false once the handshake ran (an <c>initialize</c> without the group means no images), <see langword="null"/> before it.</summary>
    public bool? SupportsImages
    {
        get
        {
            lock (_gate)
            {
                return _supportsImages;
            }
        }
    }

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
        ValidateSubmit(prompt, options);

        var (hookedPrompt, refusal) = await ApplyBeforePromptGateAsync(prompt, cancellationToken).ConfigureAwait(false);
        if (refusal is not null)
        {
            foreach (var refused in refusal)
            {
                yield return refused;
            }

            yield break;
        }

        var turn = BeginTurn();
        var finished = false;
        try
        {
            await StartPromptAsync(turn, hookedPrompt, options.Resources, options.Images, cancellationToken).ConfigureAwait(false);
            while (true)
            {
                var held = new HeldTerminalEvent(_hookGate is not null);
                await foreach (var harnessEvent in turn.Events.Reader.ReadAllAsync(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    // With a hook gate the terminal event is held back until the stop hooks have run: a follow-up continues the same stream.
                    if (!held.TryHold(harnessEvent))
                    {
                        yield return harnessEvent;
                    }
                }

                var (notice, followUp, final) = await EndHeldTurnAsync(held, cancellationToken).ConfigureAwait(false);
                if (followUp is not null)
                {
                    if (!string.IsNullOrEmpty(notice))
                    {
                        yield return new DiagnosticEvent(notice);
                    }

                    turn = BeginTurn();
                    await StartPromptAsync(turn, followUp, [], [], cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (final is not null)
                {
                    yield return final;
                }

                finished = true;
                break;
            }
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

    /// <inheritdoc />
    public async Task<OmpModelSwitchResult> SetModelAsync(string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string sessionId;
        string optionId;
        lock (_gate)
        {
            if (RefuseModelSwitchLocked(value) is { } refusal)
            {
                return refusal;
            }

            if (string.Equals(_currentModelId, value, StringComparison.Ordinal))
            {
                // The engine already uses this model: nothing to ask.
                return new OmpModelSwitchResult(OmpModelSwitchOutcome.Switched, _currentModelId, null);
            }

            _optionChanging = true;
            sessionId = SessionId!;
            optionId = _modelOptionId!;
        }

        var failure = await SendOptionChangeAsync(sessionId, optionId, value, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return new OmpModelSwitchResult(OmpModelSwitchOutcome.Failed, CurrentModelId, $"omp could not switch the model: {failure}");
        }

        var current = CurrentModelId;
        return string.Equals(current, value, StringComparison.Ordinal)
            ? new OmpModelSwitchResult(OmpModelSwitchOutcome.Switched, current, null)
            : new OmpModelSwitchResult(OmpModelSwitchOutcome.Failed, current, "omp did not confirm the model change.");
    }

    /// <inheritdoc />
    public async Task<OmpOptionSetResult> SetOptionAsync(string configId, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configId);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string sessionId;
        lock (_gate)
        {
            if (RefuseOptionSetLocked(configId, value) is { } refusal)
            {
                return refusal;
            }

            var option = _configOptions.First(candidate => string.Equals(candidate.Id, configId, StringComparison.Ordinal));
            if (string.Equals(option.CurrentValue, value, StringComparison.Ordinal))
            {
                // The engine already has this value: nothing to ask.
                return new OmpOptionSetResult(OmpOptionSetOutcome.Set, option.CurrentValue, null);
            }

            _optionChanging = true;
            sessionId = SessionId!;
        }

        var failure = await SendOptionChangeAsync(sessionId, configId, value, cancellationToken).ConfigureAwait(false);
        var current = CurrentOptionValue(configId);
        if (failure is not null)
        {
            return new OmpOptionSetResult(OmpOptionSetOutcome.Failed, current, $"omp could not change the setting: {failure}");
        }

        return string.Equals(current, value, StringComparison.Ordinal)
            ? new OmpOptionSetResult(OmpOptionSetOutcome.Set, current, null)
            : new OmpOptionSetResult(OmpOptionSetOutcome.Failed, current, "omp did not confirm the change.");
    }

    /// <summary>
    /// Puts a message into the turn that is running, with the omp extension request <c>_session/steering</c>. Nothing is sent when omp did not
    /// advertise <c>_meta.steering.supported</c> (<see cref="AcpSteerOutcome.Unsupported"/>), when the session is closed or this client has
    /// no prompt in flight (<see cref="AcpSteerOutcome.NotRunning"/>), or when the text is blank or longer than 8 KiB
    /// (<see cref="AcpSteerOutcome.Failed"/>). omp never starts a turn from this request: <see cref="AcpSteerOutcome.PromptRequired"/> means the
    /// caller must submit the text with <see cref="SubmitPromptAsync"/> itself.
    /// </summary>
    /// <param name="text">The message.</param>
    /// <param name="queue">The omp queue to put the message into.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>How the attempt ended.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="queue"/> is not a defined value.</exception>
    public async Task<AcpSteerResult> SteerAsync(string text, AcpSteerLane queue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!Enum.IsDefined(queue))
        {
            throw new ArgumentOutOfRangeException(nameof(queue));
        }

        string sessionId;
        lock (_gate)
        {
            if (!_steeringSupported)
            {
                return new AcpSteerResult(AcpSteerOutcome.Unsupported, queue, "This version of omp cannot take a message into a running turn.");
            }

            if (_disposed || _sessionClosed || _connectionLost || SessionId is null || _machine.State != HarnessState.Streaming || _turn is not { Settled: false })
            {
                return new AcpSteerResult(AcpSteerOutcome.NotRunning, queue, "No turn is running.");
            }

            sessionId = SessionId;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new AcpSteerResult(AcpSteerOutcome.Failed, queue, "The message is empty.");
        }

        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxSteerBytes)
        {
            return new AcpSteerResult(AcpSteerOutcome.Failed, queue, "The message is too long: the limit is 8 KiB.");
        }

        var parameters = new AcpFrames.SteeringParams(
            sessionId,
            [new AcpFrames.ContentBlock { Text = text }],
            queue == AcpSteerLane.FollowUp ? new AcpFrames.SteeringMeta(new AcpFrames.SteeringQueueMeta(FollowUpQueueName)) : null);
        AcpFrames.SteeringResult answer;
        try
        {
            var response = await CallAsync(SteeringMethod, parameters, AcpJsonContext.Default.RequestSteeringParams, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);
            answer = ReadResult(response, AcpJsonContext.Default.SteeringResult, SteeringMethod);
        }
        catch (HarnessException ex)
        {
            return new AcpSteerResult(AcpSteerOutcome.Failed, queue, DescribeSteerFailure(ex));
        }

        return MapSteerAnswer(answer, queue);
    }

    private static AcpSteerResult MapSteerAnswer(AcpFrames.SteeringResult answer, AcpSteerLane requested)
    {
        var reported = answer.Queue switch
        {
            FollowUpQueueName => AcpSteerLane.FollowUp,
            SteeringQueueName => AcpSteerLane.Steering,
            _ => requested,
        };
        return answer.Outcome switch
        {
            "injected" => new AcpSteerResult(AcpSteerOutcome.Injected, reported, null),
            "promptRequired" => new AcpSteerResult(AcpSteerOutcome.PromptRequired, reported, null),
            "failed" => new AcpSteerResult(AcpSteerOutcome.Failed, reported, string.IsNullOrWhiteSpace(answer.Reason) ? "omp could not take the message." : answer.Reason),
            _ => new AcpSteerResult(AcpSteerOutcome.Failed, reported, "omp answered the message in a way this app does not understand."),
        };
    }

    /// <inheritdoc />
    async Task<OmpSteerResult> IOmpLiveSteering.SteerAsync(string text, OmpSteerLane queue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!Enum.IsDefined(queue))
        {
            throw new ArgumentOutOfRangeException(nameof(queue));
        }

        var result = await SteerAsync(text, queue == OmpSteerLane.FollowUp ? AcpSteerLane.FollowUp : AcpSteerLane.Steering, cancellationToken).ConfigureAwait(false);
        var outcome = result.Outcome switch
        {
            AcpSteerOutcome.Injected => OmpSteerOutcome.Injected,
            AcpSteerOutcome.PromptRequired => OmpSteerOutcome.PromptRequired,
            AcpSteerOutcome.Unsupported => OmpSteerOutcome.Unsupported,
            AcpSteerOutcome.NotRunning => OmpSteerOutcome.NotRunning,
            _ => OmpSteerOutcome.Failed,
        };
        return new OmpSteerResult(outcome, result.Queue == AcpSteerLane.FollowUp ? OmpSteerLane.FollowUp : OmpSteerLane.Steering, result.Reason);
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

        await CloseRemoteAsync(sessionId, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);
        await NotifySessionClosedAsync("user_close").ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool CanFork => AdvertisedCapabilities.TryGetValue("sessionCapabilities.fork", out var advertised) && string.Equals(advertised, "true", StringComparison.Ordinal);

    /// <inheritdoc />
    /// <remarks>
    /// Sends <c>session/fork</c> (the unstable <c>unstable_forkSession</c> request) with this session's identifier, working directory and MCP servers. The agent keeps
    /// this session and answers with the identifier of the new one; the new session is not bound here.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The session is not open, or a prompt is in flight.</exception>
    /// <exception cref="AcpRequestFailedHarnessException">The agent answered <c>session/fork</c> with an error or without a sessionId.</exception>
    public async Task<string> ForkAsync(CancellationToken cancellationToken)
    {
        if (!CanFork)
        {
            throw new NotSupportedException("This omp does not advertise forking a session.");
        }

        string sessionId;
        lock (_gate)
        {
            if (_disposed || _sessionClosed || SessionId is null)
            {
                throw new InvalidOperationException("The ACP session is not open, so it cannot be forked.");
            }

            if (_machine.State is HarnessState.Streaming or HarnessState.Cancelling)
            {
                throw new InvalidOperationException("The ACP session cannot be forked while a prompt is in flight.");
            }

            sessionId = SessionId;
        }

        var response = await CallAsync(
            "session/fork",
            new AcpFrames.ForkSessionParams(sessionId, _options.WorkingDirectory, _mcpServers),
            AcpJsonContext.Default.RequestForkSessionParams,
            _options.RequestTimeout,
            cancellationToken).ConfigureAwait(false);
        var forked = ReadResult(response, AcpJsonContext.Default.NewSessionResult, "session/fork");
        if (forked.SessionId.Length == 0)
        {
            throw new AcpRequestFailedHarnessException("omp acp forked a session without a sessionId.", "session/fork");
        }

        return forked.SessionId;
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

        await NotifySessionClosedAsync("user_close").ConfigureAwait(false);
        if (closeRemote && sessionId is not null)
        {
            try
            {
                await CloseRemoteAsync(sessionId, DisposeCloseTimeout, CancellationToken.None).ConfigureAwait(false);
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

    /// <summary>Decides, with the gate held, whether a switch must be refused before anything is sent. Null means the switch may go ahead.</summary>
    private OmpModelSwitchResult? RefuseModelSwitchLocked(string value)
    {
        if (_disposed || _sessionClosed || _connectionLost || SessionId is null || _machine.State is not (HarnessState.Ready or HarnessState.Streaming or HarnessState.Cancelling))
        {
            return new OmpModelSwitchResult(OmpModelSwitchOutcome.NotOpen, _currentModelId, "The agent session is not open, so the model cannot be changed.");
        }

        // omp's own handler does not reject a switch mid-turn, but the turn runs on the model it started with; a client that lets the
        // change land mid-turn would show a model that is not the one answering, so a running prompt refuses the switch here.
        if (_machine.State != HarnessState.Ready || _turn is not null || _optionChanging)
        {
            return new OmpModelSwitchResult(OmpModelSwitchOutcome.Busy, _currentModelId, "The model cannot be changed while a response is running.");
        }

        if (_modelOptionId is null || _currentModelId is null)
        {
            return new OmpModelSwitchResult(OmpModelSwitchOutcome.NoModelOption, _currentModelId, "omp offers no model choice for this session.");
        }

        if (!_modelChoices.Any(choice => string.Equals(choice.Value, value, StringComparison.Ordinal)))
        {
            return new OmpModelSwitchResult(OmpModelSwitchOutcome.UnknownModel, _currentModelId, "omp does not offer that model.");
        }

        return null;
    }

    /// <summary>Decides, with the gate held, whether a change of an option must be refused before anything is sent. Null means the change may go ahead.</summary>
    private OmpOptionSetResult? RefuseOptionSetLocked(string configId, string value)
    {
        var current = _configOptions.FirstOrDefault(candidate => string.Equals(candidate.Id, configId, StringComparison.Ordinal));
        if (_disposed || _sessionClosed || _connectionLost || SessionId is null || _machine.State is not (HarnessState.Ready or HarnessState.Streaming or HarnessState.Cancelling))
        {
            return new OmpOptionSetResult(OmpOptionSetOutcome.NotOpen, current?.CurrentValue, "The agent session is not open, so the setting cannot be changed.");
        }

        // A running prompt keeps the settings it started with, for the same reason as the model.
        if (_machine.State != HarnessState.Ready || _turn is not null || _optionChanging)
        {
            return new OmpOptionSetResult(OmpOptionSetOutcome.Busy, current?.CurrentValue, "The setting cannot be changed while a response is running.");
        }

        if (current is null)
        {
            return new OmpOptionSetResult(OmpOptionSetOutcome.UnknownOption, null, "omp offers no such setting for this session.");
        }

        if (!current.Values.Any(candidate => string.Equals(candidate.Value, value, StringComparison.Ordinal)))
        {
            return new OmpOptionSetResult(OmpOptionSetOutcome.UnknownValue, current.CurrentValue, "omp does not offer that value.");
        }

        return null;
    }

    /// <summary>The wire part both the model switch and an option change share: <c>session/set_config_option</c>, then the option list omp answered with. Returns the failure text, or null when omp answered. It always clears the in-flight flag.</summary>
    private async Task<string?> SendOptionChangeAsync(string sessionId, string optionId, string value, CancellationToken cancellationToken)
    {
        try
        {
            var response = await CallAsync(
                SetConfigOptionMethod,
                new AcpFrames.SetConfigOptionParams(sessionId, optionId, value),
                AcpJsonContext.Default.RequestSetConfigOptionParams,
                _options.RequestTimeout,
                cancellationToken).ConfigureAwait(false);
            ApplyConfigOptions(ReadResult(response, AcpJsonContext.Default.ConfigOptionsResult, SetConfigOptionMethod).ConfigOptions);
            return null;
        }
        catch (HarnessException ex)
        {
            return ex.Message;
        }
        finally
        {
            lock (_gate)
            {
                _optionChanging = false;
            }
        }
    }

    private string? CurrentOptionValue(string configId)
    {
        lock (_gate)
        {
            return _configOptions.FirstOrDefault(candidate => string.Equals(candidate.Id, configId, StringComparison.Ordinal))?.CurrentValue;
        }
    }

    /// <summary>Reads <c>_meta.steering.supported</c> of the <c>initialize</c> result. Anything but a boolean <c>true</c> at that place means no support.</summary>
    private static bool ReadSteeringSupported(JsonElement? meta) =>
        meta is { ValueKind: JsonValueKind.Object } owner
        && owner.TryGetProperty("steering", out var steering)
        && steering.ValueKind == JsonValueKind.Object
        && steering.TryGetProperty("supported", out var supported)
        && supported.ValueKind == JsonValueKind.True;

    /// <summary>Turns a failed steering request into a sentence a person can read: no type names, no wire jargon.</summary>
    private static string DescribeSteerFailure(HarnessException failure) => failure switch
    {
        AcpRequestFailedHarnessException { TimedOut: true } => "omp did not answer in time.",
        AcpRequestFailedHarnessException { ErrorCode: InvalidParams } => "omp no longer has this session open, so the message was not sent.",
        AcpRequestFailedHarnessException { ErrorCode: MethodNotFound } => "This version of omp cannot take a message into a running turn.",
        AcpRequestFailedHarnessException { ErrorCode: not null } => "omp refused the message.",
        DisconnectedHarnessException => "The connection to omp was lost.",
        _ => "omp did not take the message.",
    };

    /// <summary>
    /// Replaces the stored options with the agent's report and raises <see cref="ModelChanged"/> when the model or the model list differs. A
    /// report without a <c>configOptions</c> array leaves what the session knows as it is.
    /// </summary>
    private void ApplyConfigOptions(JsonElement? raw)
    {
        if (raw is not { ValueKind: JsonValueKind.Array })
        {
            return;
        }

        var options = AcpConfigOptionParser.Parse(raw);
        var model = options.FirstOrDefault(static option => string.Equals(option.Id, ModelOptionId, StringComparison.Ordinal))
            ?? options.FirstOrDefault(static option => string.Equals(option.Category, ModelOptionId, StringComparison.Ordinal));
        OmpModelChoice[] choices = model is null ? [] : model.Values.Select(static value => new OmpModelChoice(value.Value, value.Name)).ToArray();
        var sessionOptions = options.Select(static option => new OmpSessionOption(
            option.Id,
            option.Name,
            option.Category,
            option.CurrentValue,
            option.Values.Select(static value => new OmpSessionOptionValue(value.Value, value.Name, value.Description)).ToArray())).ToArray();
        bool changed;
        bool optionsChanged;
        lock (_gate)
        {
            changed = !string.Equals(_currentModelId, model?.CurrentValue, StringComparison.Ordinal) || !_modelChoices.SequenceEqual(choices);
            optionsChanged = _sessionOptions.Length != sessionOptions.Length || !_sessionOptions.Zip(sessionOptions, static (known, reported) => known.IsSameAs(reported)).All(static same => same);
            _configOptions = options;
            _modelChoices = choices;
            _modelOptionId = model?.Id;
            _currentModelId = model?.CurrentValue;
            _sessionOptions = sessionOptions;
        }

        if (changed)
        {
            RaiseModelChanged();
        }

        if (optionsChanged)
        {
            RaiseOptionsChanged();
        }
    }

    /// <summary>
    /// Replaces the stored commands with the agent's report and raises <see cref="CommandsChanged"/> when the list differs. A report without an
    /// <c>availableCommands</c> array leaves what the session knows as it is; an empty array replaces the list with an empty one.
    /// </summary>
    private void ApplyAvailableCommands(JsonElement? raw)
    {
        if (raw is not { ValueKind: JsonValueKind.Array })
        {
            return;
        }

        var parsed = AcpAvailableCommandParser.Parse(raw);
        var commands = parsed.Select(static command => new OmpCommand(command.Name, command.Description, command.InputHint)).ToArray();
        bool changed;
        lock (_gate)
        {
            changed = !_commands.SequenceEqual(commands);
            _availableCommands = parsed;
            _commands = commands;
        }

        if (changed)
        {
            RaiseCommandsChanged();
        }
    }

    /// <summary>Raises <see cref="ModelChanged"/>. A subscriber that throws never fails the pump or the caller, and never hides the change from the other subscribers.</summary>
    private void RaiseModelChanged()
    {
        if (ModelChanged is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never fail the session.
            }
        }
    }

    /// <summary>Raises <see cref="OptionsChanged"/>. A subscriber that throws never fails the pump or the caller, and never hides the change from the other subscribers.</summary>
    private void RaiseOptionsChanged()
    {
        if (OptionsChanged is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never fail the session.
            }
        }
    }

    /// <summary>Raises <see cref="CommandsChanged"/>. A subscriber that throws never fails the pump or the caller, and never hides the change from the other subscribers.</summary>
    private void RaiseCommandsChanged()
    {
        if (CommandsChanged is not { } handlers)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never fail the session.
            }
        }
    }

    /// <summary>
    /// Applies a <c>config_option_update</c> notification, in any session state: omp pushes one after a model or thinking change, also
    /// one that a slash command made, and while a prompt runs. Other updates and other sessions' updates are ignored.
    /// </summary>
    private void TryApplyConfigOptionUpdate(AcpFrames.Inbound inbound)
    {
        if (inbound.Params is not { ValueKind: JsonValueKind.Object } parameters
            || !parameters.TryGetProperty("update", out var update)
            || update.ValueKind != JsonValueKind.Object
            || !update.TryGetProperty("sessionUpdate", out var kind)
            || !kind.ValueEquals(ConfigOptionUpdateKind))
        {
            return;
        }

        var current = SessionId;
        if (current is not null
            && !(parameters.TryGetProperty("sessionId", out var target) && target.ValueKind == JsonValueKind.String && target.ValueEquals(current)))
        {
            return;
        }

        ApplyConfigOptions(update.TryGetProperty("configOptions", out var options) ? options : null);
    }

    /// <summary>
    /// Applies an <c>available_commands_update</c> notification, in any session state: omp advertises its commands right after <c>session/new</c>
    /// and again after a plugin reload, also while a prompt runs. Other updates and other sessions' updates are ignored.
    /// </summary>
    private void TryApplyAvailableCommandsUpdate(AcpFrames.Inbound inbound)
    {
        if (inbound.Params is not { ValueKind: JsonValueKind.Object } parameters
            || !parameters.TryGetProperty("update", out var update)
            || update.ValueKind != JsonValueKind.Object
            || !update.TryGetProperty("sessionUpdate", out var kind)
            || !kind.ValueEquals(AvailableCommandsUpdateKind))
        {
            return;
        }

        var current = SessionId;
        if (current is not null
            && !(parameters.TryGetProperty("sessionId", out var target) && target.ValueKind == JsonValueKind.String && target.ValueEquals(current)))
        {
            return;
        }

        ApplyAvailableCommands(update.TryGetProperty("availableCommands", out var commands) ? commands : null);
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
            await NotifySessionOpenedAsync(resumeSessionId is not null, cancellationToken).ConfigureAwait(false);
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
            _steeringSupported = ReadSteeringSupported(result.Meta);
            _supportsImages = result.AgentCapabilities.PromptCapabilities?.Image ?? false;
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
                "omp asked this app to sign in, but no sign-in method is set up for it.",
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
                $"This app's sign-in method '{methodId}' is not one that omp offers.",
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
                new AcpFrames.NewSessionParams(cwd, _mcpServers),
                AcpJsonContext.Default.RequestNewSessionParams,
                _options.RequestTimeout,
                cancellationToken).ConfigureAwait(false);
            var created = ReadResult(response, AcpJsonContext.Default.NewSessionResult, "session/new");
            if (created.SessionId.Length == 0)
            {
                throw new AcpRequestFailedHarnessException("omp acp created a session without a sessionId.", "session/new");
            }

            SetSessionId(created.SessionId);
            ApplyConfigOptions(created.ConfigOptions);
            return;
        }

        var loaded = await CallAsync(
            "session/load",
            new AcpFrames.LoadSessionParams(resumeSessionId, cwd, _mcpServers),
            AcpJsonContext.Default.RequestLoadSessionParams,
            _options.RequestTimeout,
            cancellationToken).ConfigureAwait(false);
        SetSessionId(resumeSessionId);

        // A load answer may carry no result at all; the options are then learned from the agent's own config_option_update.
        ApplyConfigOptions(loaded.Result is { ValueKind: JsonValueKind.Object } result && result.TryGetProperty("configOptions", out var options) ? options : null);
    }

    private async Task CloseRemoteAsync(string sessionId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await CallAsync(
            "session/close",
            new AcpFrames.CloseSessionParams(sessionId),
            AcpJsonContext.Default.RequestCloseSessionParams,
            timeout,
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

            // The transport ends the pump with an exception on every close, including one this session requested.
            // Only a fault on a session that is still open is an error.
            bool requested;
            lock (_gate)
            {
                requested = _disposed || _sessionClosed || _openFailed;
            }

            if (!requested)
            {
                PumpFailed(_logger, SessionId, ex);
            }
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
            else if (string.Equals(inbound.Method, AcpHookNotificationFrames.Method, StringComparison.Ordinal))
            {
                HandleHookNotification(inbound);
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
        TryApplyConfigOptionUpdate(inbound);
        TryApplyAvailableCommandsUpdate(inbound);
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

            _hookSignals?.OnUpdate(notification.Update);

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

    private static AcpFrames.PermissionRequestParams? ParsePermissionParams(JsonElement? raw)
    {
        try
        {
            return raw?.Deserialize(AcpJsonContext.Default.PermissionRequestParams);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<bool> HookDeniesPermissionAsync(long id, AcpFrames.PermissionRequestParams parameters, AcpPermissionRequest permission, PromptTurn? turn)
    {
        if (_hookGate is null)
        {
            return false;
        }

        var hookVerdict = await AskGateAsync("BeforePermission", gate => gate.BeforePermissionAsync(permission, CancellationToken.None)).ConfigureAwait(false);
        if (!hookVerdict.Denied)
        {
            return false;
        }

        if (turn is not null)
        {
            WriteTurnEvent(turn, new DiagnosticEvent($"A hook denied permission request for call '{permission.CallId}': {hookVerdict.DenialText}"));
        }

        await SendPermissionOutcomeAsync(id, parameters.Options, granted: false, CancellationToken.None).ConfigureAwait(false);
        return true;
    }

    private async Task AnswerPermissionAsync(long id, AcpFrames.Inbound request)
    {
        var raw = request.Params;
        var parameters = ParsePermissionParams(raw);

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

        // A hook may deny before the approval policy is consulted. It can only tighten: the policy still decides everything it admits.
        if (await HookDeniesPermissionAsync(id, parameters, permission, turn).ConfigureAwait(false))
        {
            return;
        }

        // The policy ceiling is consulted first. A request it forbids never becomes an approval: the turn records an informational
        // diagnostic and the agent gets its reject answer at once, so no run registers or waits on the request.
        var granted = IsGrantedByPolicy(permission, turn);
        if (!granted)
        {
            if (turn is not null)
            {
                WriteTurnEvent(turn, new DiagnosticEvent($"Approval policy denied permission request for call '{permission.CallId}' without a decision."));
            }

            await SendPermissionOutcomeAsync(id, parameters.Options, granted: false, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        // The policy admits the request, so it becomes a pending approval that a decision settles.
        var requestEvent = new PermissionRequestEvent(permission.CallId, permission.Name, permission.PayloadJson, id.ToString(CultureInfo.InvariantCulture));
        if (turn is not null)
        {
            WriteTurnEvent(turn, requestEvent);
        }

        if (_permissionResolver is { } resolver)
        {
            // Park the request under its id and keep the pump reading; the resolver's decision answers it.
            _ = DecideParkedPermissionAsync(id, parameters.Options, requestEvent, resolver, turn);
            return;
        }

        // An admitted request with no resolver to decide it is denied.
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

        if (_hookGate is not null)
        {
            var hookVerdict = await AskGateAsync("BeforeReadFile", gate => gate.BeforeReadFileAsync(fullPath, string.Join("\n", lines), CancellationToken.None)).ConfigureAwait(false);
            if (hookVerdict.Denied)
            {
                await SendFsErrorAsync(id, InvalidParams, $"{FsReadMethod} was denied by a hook: {hookVerdict.DenialText}").ConfigureAwait(false);
                return;
            }
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

        var previousContent = _hookGate is null ? null : await ReadPreviousContentAsync(fullPath).ConfigureAwait(false);

        if (!await TryWriteFsFileAsync(id, fullPath, parameters.Content).ConfigureAwait(false))
        {
            return;
        }

        await NotifyFileWrittenAsync(fullPath, previousContent, parameters.Content).ConfigureAwait(false);

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

    private async Task StartPromptAsync(PromptTurn turn, string prompt, IReadOnlyList<PromptResource> resources, IReadOnlyList<PromptImage> images, CancellationToken cancellationToken)
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

            if (resources.Count == 0 && images.Count == 0)
            {
                var parameters = new AcpFrames.PromptParams(turn.SessionId, [new AcpFrames.ContentBlock { Text = prompt }]);
                await SendFrameAsync(
                    new AcpFrames.Request<AcpFrames.PromptParams>(id, "session/prompt", parameters),
                    AcpJsonContext.Default.RequestPromptParams,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Mentions travel as embedded resource blocks: omp inlines them, where it reduces a resource link to a title. Images follow them as image blocks.
                AcpPromptResource[] mentioned = [.. resources.Select(static resource => new AcpPromptResource(resource.Location.AbsoluteUri, resource.MimeType, resource.Text, resource.DisplayName, resource.OriginalLength, resource.Truncated))];
                var content = images.Count == 0
                    ? AcpPromptContent.BuildParams(turn.SessionId, prompt, mentioned)
                    : AcpPromptContent.BuildParamsWithImages(
                        turn.SessionId,
                        prompt,
                        mentioned,
                        [.. images.Select(static image => new AcpPromptImage(image.Name, image.MimeType, image.Data))]);
                await SendFrameAsync(
                    new AcpFrames.Request<AcpPromptContentParams>(id, "session/prompt", content),
                    AcpPromptContentJsonContext.Default.RequestAcpPromptContentParams,
                    cancellationToken).ConfigureAwait(false);
            }
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
        _hookSignals?.FlushText();
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
            _openFailed = true;
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

    private async Task NotifySessionOpenedAsync(bool resumed, CancellationToken cancellationToken)
    {
        if (_hookGate is not { } gate || SessionId is not { } sessionId)
        {
            return;
        }

        try
        {
            await gate.SessionOpenedAsync(sessionId, resumed, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or OperationCanceledException))
        {
            HookGateFailed(_logger, "SessionOpened", ex.GetType().Name, null);
        }
    }

    /// <summary>Tells the gate the session ended, once, with a bound so a slow <c>sessionEnd</c> hook cannot hold the disposal.</summary>
    private async Task NotifySessionClosedAsync(string reason)
    {
        if (_hookGate is not { } gate || SessionId is null || Interlocked.Exchange(ref _endNotified, 1) != 0)
        {
            return;
        }

        using var limit = new CancellationTokenSource(HookSessionEndTimeout);
        try
        {
            await gate.SessionClosedAsync(reason, limit.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            HookGateFailed(_logger, "SessionClosed", ex.GetType().Name, null);
        }
    }

    private void ValidateSubmit(string prompt, PromptOptions options)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Steer)
        {
            throw new NotSupportedException("ACP v1 has no steer operation. Cancel the in-flight prompt, then submit.");
        }

        // An image is never dropped silently: an engine that said image false refuses the prompt before anything is sent.
        if (options.Images.Count > 0 && SupportsImages == false)
        {
            throw new NotSupportedException("omp does not accept images in a prompt: its initialize result says promptCapabilities.image is false.");
        }
    }

    /// <summary>Runs the <c>BeforePrompt</c> hooks. Returns the prompt with the hook context in front of it, or the events that refuse the prompt.</summary>
    private async Task<(string Prompt, HarnessEvent[]? Refusal)> ApplyBeforePromptGateAsync(string prompt, CancellationToken cancellationToken)
    {
        if (_hookGate is null)
        {
            return (prompt, null);
        }

        var verdict = await AskGateAsync("BeforePrompt", gate => gate.BeforePromptAsync(prompt, cancellationToken)).ConfigureAwait(false);
        if (verdict.Denied)
        {
            return (prompt, [new DiagnosticEvent("A hook stopped this prompt: " + verdict.DenialText), new CompletedEvent("refusal")]);
        }

        return (string.IsNullOrEmpty(verdict.PromptContext) ? prompt : verdict.PromptContext + "\n\n" + prompt, null);
    }

    /// <summary>Runs the stop hooks for a turn whose terminal event was held. Returns the follow-up prompt (and the notice to show) when the hooks continue the turn, else the terminal event to deliver, or nothing when the stream ended without one.</summary>
    private async Task<(string? Notice, string? FollowUp, HarnessEvent? Final)> EndHeldTurnAsync(HeldTerminalEvent held, CancellationToken cancellationToken)
    {
        var completed = held.Completed;
        var failed = held.Failed;
        if (completed is null && failed is null)
        {
            return (null, null, null);
        }

        var decision = await EndTurnWithGateAsync(completed?.Reason ?? "error", cancellationToken).ConfigureAwait(false);
        if (completed is not null && !string.IsNullOrEmpty(decision.FollowUpPrompt))
        {
            return (decision.Notice, decision.FollowUpPrompt, null);
        }

        return (null, null, (HarnessEvent?)completed ?? failed);
    }

    /// <summary>Holds back the terminal event of a turn while the stop hooks decide whether the turn continues.</summary>
    private sealed class HeldTerminalEvent(bool enabled)
    {
        /// <summary>Gets the held completion, or null.</summary>
        public CompletedEvent? Completed { get; private set; }

        /// <summary>Gets the held error, or null.</summary>
        public ErrorEvent? Failed { get; private set; }

        /// <summary>Holds a terminal event when holding is enabled. Returns true when the event was held.</summary>
        public bool TryHold(HarnessEvent harnessEvent)
        {
            if (!enabled)
            {
                return false;
            }

            switch (harnessEvent)
            {
                case CompletedEvent done:
                    Completed = done;
                    return true;
                case ErrorEvent error:
                    Failed = error;
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>Runs the stop hooks of a turn that ended. A failing gate ends the turn as it ended.</summary>
    private async Task<AcpTurnEndDecision> EndTurnWithGateAsync(string stopReason, CancellationToken cancellationToken)
    {
        if (_hookGate is not { } gate)
        {
            return AcpTurnEndDecision.End;
        }

        try
        {
            return await gate.TurnEndedAsync(stopReason, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or OperationCanceledException))
        {
            HookGateFailed(_logger, "TurnEnded", ex.GetType().Name, null);
            return AcpTurnEndDecision.End;
        }
    }

    /// <summary>Asks the gate a question. A gate that throws allows the action: the failure policy of a hook (<c>failClosed</c>) lives inside the gate.</summary>
    private async Task<AcpHookVerdict> AskGateAsync(string point, Func<IAcpHookGate, ValueTask<AcpHookVerdict>> ask)
    {
        try
        {
            return await ask(_hookGate!).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not (OutOfMemoryException or OperationCanceledException))
        {
            HookGateFailed(_logger, point, ex.GetType().Name, null);
            return AcpHookVerdict.Allow;
        }
    }

    /// <summary>Reads the text a file has before the agent overwrites it, for the <c>afterFileEdit</c> hooks. Null when the file does not exist or is too large to compare.</summary>
    private static async Task<string?> ReadPreviousContentAsync(string fullPath)
    {
        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists || info.Length > MaxHookCompareBytes)
            {
                return null;
            }

            return await File.ReadAllTextAsync(fullPath, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task NotifyFileWrittenAsync(string fullPath, string? previousContent, string content)
    {
        if (_hookGate is not { } gate)
        {
            return;
        }

        try
        {
            await gate.AfterFileWriteAsync(fullPath, previousContent, content, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            HookGateFailed(_logger, "AfterFileWrite", ex.GetType().Name, null);
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

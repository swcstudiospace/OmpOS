using System.Globalization;
using Microsoft.Extensions.Logging;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Connection;
using OmpOS.Harness.AgentLoop.Gsd;
using OmpOS.Harness.AgentLoop.Hooks;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Plugins;
using OmpOS.Harness.AgentLoop.Safety;
using OmpOS.Harness.AgentLoop.Sessions;
using OmpOS.Harness.AgentLoop.Stages;
using OmpOS.Harness.AgentLoop.Steering;
using OmpOS.Harness.AgentLoop.Substrate;
using OmpOS.Harness.OmpLauncher;
using OmpOS.Harness.Remote;
using OmpOS.Harness.Remote.Containers;
using OmpOS.Harness.Terminal;
using OmpOS.Native.Composition;
using OmpOS.Native.Composition.Checkpoints;
using OmpOS.Native.Composition.Mentions;
using OmpOS.Native.Engine;
using OmpOS.Native.Settings;
using OmpOS.Native.Views.Conventions;
using OmpOS.Native.Views.Sessions;
using OmpOS.Native.Views.Terminal;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Mentions;
using OmpOS.Presentation.Remote;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.SideChats;

namespace OmpOS.Native.Agents;

/// <summary>
/// The head's implementation of <see cref="IAgentPanelHost"/>: it owns the session registry and one <see cref="SessionSlot"/> per opened
/// session, so N sessions keep their own state directory, launch plan, engine (one <c>omp acp</c> child each), stage sources and runtime.
/// </summary>
/// <remarks>
/// <para>
/// The registry is opened by <see cref="ReloadAsync"/> and, the first time, every session that was running when the application last ended
/// is marked lost. A timer then refreshes the heartbeat of sessions whose turn is running and marks those that went silent as lost.
/// </para>
/// <para>
/// The registry mirrors what the runtimes report: a started turn is Running, a completed turn Completed (Created when the operator stopped
/// it), a faulted turn Failed; the omp session identifier and the Graph identifier are recorded as soon as the engine and the planner report them.
/// </para>
/// </remarks>
internal sealed class SessionEngineSet : IAgentPanelHost, IAsyncDisposable
{
    private const string RegistryFileName = "sessions.json";
    private const string CancelledReason = "cancelled";
    private const UnixFileMode OwnerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static readonly string[] SteerSubdirectories = ["inbox", "acks", "done"];

    private static readonly Action<ILogger, string, Exception?> MaintenanceFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1, "AgentMaintenanceFailed"),
        "The agent maintenance tick failed ({ExceptionType}); the next tick tries again.");

    private static readonly Action<ILogger, string, Exception?> DisposeFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(2, "AgentSessionDisposeFailed"),
        "Disposing an agent session failed ({ExceptionType}); the other sessions are still stopped.");

    private static readonly Action<ILogger, string, Exception?> TerminalNoticeFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(3, "TerminalResumeNoticeFailed"),
        "The notice that a terminal starts a new omp session could not be published ({ExceptionType}).");

    private readonly SessionEngineSetOptions _options;
    private readonly ReloadableSessionCatalog _catalog = new();
    private readonly System.Threading.Lock _gate = new();
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly Dictionary<string, SessionSlot> _slots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string[]> _additionalDirectories = new(StringComparer.Ordinal);
    private readonly HashSet<string> _closed = new(StringComparer.Ordinal);
    private readonly GsdToolsClient _gsd;
    private readonly ILogger? _logger;
    private AgentSessionRegistry? _registry;
    private IReadOnlyList<ResolvedPlugin> _plugins = [];
    private ITimer? _timer;
    private bool _disposed;
    private readonly HeadServices? _services;
    private readonly SessionExtrasStore _extras;
    private readonly OmpSideChatEngine _sideChats;
    private readonly SessionForker _forker;

    /// <summary>Creates the set. It opens nothing until <see cref="ReloadAsync"/> or the first <see cref="CreateSessionAsync"/>.</summary>
    /// <param name="options">Where things are, and the seams a test replaces.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public SessionEngineSet(SessionEngineSetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _logger = options.LoggerFactory?.CreateLogger("OmpOS.Native.Agents");
        var clock = options.TimeProvider;
        _gsd = new GsdToolsClient(new GsdProcessRunner(new GsdToolsOptions(), clock), new GsdToolsLocator(), clock);
        _services = options.Services;
        _extras = new SessionExtrasStore(options.Loop.StateRoot);
        _sideChats = new OmpSideChatEngine(this);
        _forker = new SessionForker(this);
        if (options.Services is { } services)
        {
            // /best-of-n runs one candidate session per worktree through this set; it needs the Run Everything mode, because nobody is there to approve.
            services.BestOfN = new BestOfNHost(new BestOfNCandidateRunner(this).RunAsync);
        }
    }

    /// <summary>Gets the catalog the rail model reads. It is empty until the registry is loaded.</summary>
    public ISessionCatalog Catalog => _catalog;

    /// <summary>Gets where the plugins, the state and the Substrate are found.</summary>
    public AgentLoopEnvironment Loop => _options.Loop;

    /// <summary>Gets the registry, or null before it was loaded.</summary>
    public AgentSessionRegistry? Registry
    {
        get
        {
            lock (_gate)
            {
                return _registry;
            }
        }
    }

    /// <summary>Gets the plugins as the last <see cref="ReloadAsync"/> resolved them. Empty before the first reload.</summary>
    public IReadOnlyList<ResolvedPlugin> Plugins
    {
        get
        {
            lock (_gate)
            {
                return _plugins;
            }
        }
    }

    /// <summary>Gets the current time of the set's clock.</summary>
    internal DateTimeOffset Now => _options.TimeProvider.GetUtcNow();

    /// <summary>Gets the clock of the set, which paces the steering channels of its sessions.</summary>
    internal TimeProvider Clock => _options.TimeProvider;

    /// <summary>Gets the intervals of the steering channels, or null for the defaults.</summary>
    internal FileSteeringChannelOptions? SteeringOptions => _options.SteeringOptions;

    /// <summary>Gets how long a steering message waits for omp to acknowledge it.</summary>
    internal TimeSpan SteerAckTimeout => _options.SteerAckTimeout;

    /// <summary>Returns whether the operator closed a session in this application, or left it closed when it last ended, so no engine is attached.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>True when the session is closed.</returns>
    public bool IsClosed(string sessionId)
    {
        lock (_gate)
        {
            return _closed.Contains(sessionId);
        }
    }

    /// <inheritdoc />
    public async Task<string> CreateSessionAsync(CancellationToken cancellationToken)
    {
        var registry = await EnsureRegistryAsync(cancellationToken).ConfigureAwait(false);
        var (workspaceRoot, extras) = TakePendingTarget();
        var id = PrepareSession(registry, registry.Create(title: null, workspaceRoot));
        await RememberExtrasAsync(id, extras, cancellationToken).ConfigureAwait(false);
        return id;
    }

    /// <summary>
    /// Creates a session of kind terminal in the registry, titled "omp terminal N", and its private directories. No omp starts here: the
    /// terminal view starts it through the session's planner-built launch, the same plugin set as a chat session.
    /// </summary>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The identifier of the new session.</returns>
    public async Task<string> CreateTerminalSessionAsync(CancellationToken cancellationToken)
    {
        var registry = await EnsureRegistryAsync(cancellationToken).ConfigureAwait(false);
        var number = registry.SnapshotRecords().Count(static record => record.Kind == SessionKind.Terminal) + 1;
        var title = string.Create(CultureInfo.InvariantCulture, $"omp terminal {number}");

        // A terminal runs omp on this machine: whatever environment the new-session picker chose is not used and must not carry over to the next session.
        _ = _services?.NewSessionTarget.Take();
        _ = _services?.NewSessionTarget.TakeRoots();
        return PrepareSession(registry, registry.Create(title, _options.Loop.WorkspaceRoot, SessionKind.Terminal));
    }

    /// <summary>
    /// Creates the chat session of one automation run in the registry: titled with the automation's name, local, in <paramref name="workspaceRoot"/>. Unlike <see cref="CreateSessionAsync"/> it leaves the
    /// environment and roots the user chose in the New session picker alone, because the run is not that session. No omp starts here.
    /// </summary>
    /// <param name="title">The title of the session.</param>
    /// <param name="workspaceRoot">The absolute workspace root the run works in.</param>
    /// <param name="cancellationToken">Cancels the creation.</param>
    /// <returns>The identifier of the new session.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> or <paramref name="workspaceRoot"/> is blank.</exception>
    internal async Task<string> CreateAutomationSessionAsync(string title, string workspaceRoot, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        var registry = await EnsureRegistryAsync(cancellationToken).ConfigureAwait(false);
        var id = PrepareSession(registry, registry.Create(title, workspaceRoot));
        await RememberExtrasAsync(id, SessionExtras.Local, cancellationToken).ConfigureAwait(false);
        return id;
    }

    private static string PrepareSession(AgentSessionRegistry registry, AgentSessionRecord record)
    {
        try
        {
            EnsureSessionLayout(record.StateDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            registry.Close(record.Id);
            throw;
        }

        return record.Id;
    }

    /// <inheritdoc />
    public AgentSessionViewModel OpenSession(AgentRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var slot = CreateSlot(row.Id, row.Summary.WorkspaceRoot, row.GraphId);
        if (slot.Runtime is { } existing)
        {
            return existing.ViewModel;
        }

        var isTerminal = row.Summary.Kind == SessionKind.Terminal;
        var runtime = new AgentSessionRuntime(RuntimeOptions(row, slot, isTerminal));
        slot.Runtime = runtime;
        if (runtime.Terminal is { } terminal)
        {
            terminal.PropertyChanged += (_, e) =>
            {
                if (string.Equals(e.PropertyName, nameof(OmpTerminalViewModel.State), StringComparison.Ordinal))
                {
                    OnTerminalChanged(slot, terminal);
                }
            };
        }
        else
        {
            runtime.TurnChanged += (_, e) => OnTurnChanged(slot, e);
        }

        if (IsClosed(row.Id))
        {
            slot.MarkEngineClosed();
            runtime.ViewModel.MarkClosed();
        }

        runtime.StartObserving();
        return runtime.ViewModel;
    }

    private AgentSessionRuntimeOptions RuntimeOptions(AgentRow row, SessionSlot slot, bool isTerminal) => new()
    {
        Row = row,

        // A terminal session has no engine behind a prompt channel: its omp is the terminal, and the composer only steers.
        Prompts = isTerminal ? new UnconnectedPromptChannel() : slot.Prompts,
        Approvals = slot.Approvals,
        Scope = slot.Scope.Current,
        Stages = slot.Stages,
        Context = slot.Context,
        RecordSteering = slot.RecordSteeringAsync,
        Resume = cancellationToken => ReattachAsync(slot, cancellationToken),
        Reopen = cancellationToken => ReattachAsync(slot, cancellationToken),
        StageRecheck = slot.RecheckAsync,
        Motion = _options.Motion,
        TimeProvider = _options.TimeProvider,
        Steering = _options.LiveSteering || isTerminal ? slot.Steering : null,
        TerminalStarter = isTerminal ? CreateTerminalStarter(slot) : null,

        // Only a chat session has a model to choose: a terminal's omp owns its own.
        Models = isTerminal ? null : slot.Models,
        ConnectModels = isTerminal ? null : slot.Models.ConnectAsync,

        // Only a chat session has omp's own steering, mode and thinking level: a terminal steers through the inbox and its omp owns its settings.
        NativeSteering = isTerminal || !_options.LiveSteering ? null : slot.LiveSteering,
        SessionOptions = isTerminal ? null : slot.SessionOptions,

        // Only a chat session has a slash palette: a terminal's omp shows its own commands in its own terminal.
        SessionCommands = isTerminal ? null : slot.SessionCommands,

        // Custom commands, skills and subagents of the session's workspace and of the user's home, read from every convention directory when the session starts. A session on an ssh host reads them from that host.
        SlashDiscovery = isTerminal ? null : SlashDiscoveryFor(row, slot),

        // The signals of the engine side (hook notices, the connection of a remote host) reach the runtime through the slot's relay.
        LiveEvents = slot.LiveEvents,

        // @ mentions, forking and side chats belong to a chat session: a terminal has no composer prompt.
        MentionSources = isTerminal ? null : MentionSourcesFor(row),
        Forker = isTerminal ? null : _forker,
        SideChatEngine = isTerminal ? null : _sideChats,
        RemoteFiles = isTerminal || !_extras.Get(row.Id).IsRemote ? null : async cancellationToken => (await slot.RemoteResourcesAsync(cancellationToken).ConfigureAwait(false))?.FileSystem
            ?? throw new InvalidOperationException("This session has no remote host."),
    };

    private PlannedOmpTerminalStarter CreateTerminalStarter(SessionSlot slot) => new(
        slot,
        () => Registry is { } registry && registry.TryGet(slot.Id, out var record) ? record.ResumeReference : null,
        new OmpTerminalLauncher(_options.TerminalBackend ?? new LinuxLocalPtyBackend(), _options.TerminalLocator ?? OmpLocatorOptions.FromEnvironment(_options.OmpPath)),
        _options.ProviderKeys);

    /// <summary>
    /// Returns the slot of a session, creating it on first use: its live scope, send path, approval bridge and stage sources. The slot
    /// starts no process and reads no file until something asks for its plan or its engine. <see cref="OpenSession"/> builds the runtime over it.
    /// </summary>
    /// <param name="sessionId">The session, which the registry knows.</param>
    /// <param name="workspaceRoot">The workspace to use when the registry has none for the session, or null for the head's workspace.</param>
    /// <param name="graphId">The Graph identifier to use when the registry has none for the session, or null.</param>
    /// <param name="additionalDirectories">
    /// The absolute paths of the other roots of a multi-root workspace (see <c>WorkspaceSet.AdditionalDirectoryPaths</c>), in order, or null
    /// for a single-root session. They are kept for the slot's lifetime and planned as <c>--add-dir</c> arguments. Ignored when the slot exists.
    /// </param>
    /// <returns>The slot.</returns>
    /// <exception cref="ObjectDisposedException">The set was disposed.</exception>
    internal SessionSlot CreateSlot(string sessionId, string? workspaceRoot, string? graphId, IReadOnlyList<string>? additionalDirectories = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (additionalDirectories is not { Count: > 0 })
        {
            // A session reopened after a restart keeps the roots it was created with.
            additionalDirectories = _extras.Get(sessionId).AdditionalDirectories;
        }
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_slots.TryGetValue(sessionId, out var known))
            {
                return known;
            }

            var slot = new SessionSlot(this, new SessionScopeProvider(BuildScope(sessionId, workspaceRoot, graphId)));
            _slots[sessionId] = slot;
            if (additionalDirectories is { Count: > 0 })
            {
                _additionalDirectories[sessionId] = [.. additionalDirectories];
            }

            return slot;
        }
    }

    /// <inheritdoc />
    public async Task CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        SessionSlot? slot;
        lock (_gate)
        {
            _slots.TryGetValue(sessionId, out slot);
        }

        if (slot is not null)
        {
            if (slot.Runtime?.Terminal is { } terminal)
            {
                // The terminal is stopped before the engine bookkeeping so its state change runs on the caller's context; stopping it
                // disposes the omp it started, so no child outlives the closed session.
                slot.MarkEngineClosed();
                slot.SetTurnActive(false);
                await terminal.StopAsync().ConfigureAwait(false);
            }

            await slot.CloseEngineAsync().ConfigureAwait(false);
        }

        lock (_gate)
        {
            _closed.Add(sessionId);
        }

        // A session that finished stays finished; any other session that is closed can be reopened where it stopped.
        if (Registry is { } registry && registry.TryGet(sessionId, out var record))
        {
            registry.SetLifecycle(sessionId, record.Lifecycle == SessionLifecycle.Completed ? SessionLifecycle.Completed : SessionLifecycle.Paused);
        }
    }

    /// <inheritdoc />
    public Task ReloadAsync(CancellationToken cancellationToken) => ReloadCoreAsync(onlyWhenUnloaded: false, cancellationToken);

    /// <summary>Resolves the plugins again, so a plugin installed since the last check is seen.</summary>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The plugins as they are now.</returns>
    internal async Task<IReadOnlyList<ResolvedPlugin>> RefreshPluginsAsync(CancellationToken cancellationToken)
    {
        var plugins = await Task.Run(ResolvePlugins, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _plugins = plugins;
        }

        return plugins;
    }

    /// <summary>Returns the slot of an opened session, or null.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The slot.</returns>
    internal SessionSlot? SlotOf(string sessionId)
    {
        lock (_gate)
        {
            return _slots.GetValueOrDefault(sessionId);
        }
    }

    /// <summary>
    /// Decides the launch plan and the stage sources of one session. The Substrate is health-checked first, because omp fails
    /// <c>session/new</c> when it is given an MCP server it cannot reach: an unhealthy Substrate is left out of the plan and the session
    /// reports it as degraded. A failed plan builds nothing that has to be released.
    /// </summary>
    /// <param name="slot">The slot the plan is for.</param>
    /// <param name="cancellationToken">Cancels the health check.</param>
    /// <returns>The plan.</returns>
    internal async Task<SessionPlan> BuildPlanAsync(SessionSlot slot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (_options.ProviderKeys is { } providerKeys)
        {
            await providerKeys.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }

        var scope = slot.Scope.Current;
        EnsureSessionLayout(scope.SessionStateDirectory);
        var plugins = Plugins;
        var clock = _options.TimeProvider;
        var loop = _options.Loop;
        HttpClient? client = null;
        IHooksSession? hooks = null;
        RunModePolicyHolder? runModes = null;
        ConfigOverlayWriter? overlay = null;
        var extras = _extras.Get(scope.SessionId);
        try
        {
            SubstrateGateway? gateway = null;
            var reachable = false;
            string? reason = null;
            if (loop.Substrate is { } endpoint)
            {
                client = CreateSubstrateClient();
                gateway = new SubstrateGateway(client, endpoint, loop.ReadSubstrateToken, clock, _logger);
                (reachable, reason) = await CheckHealthAsync(gateway, cancellationToken).ConfigureAwait(false);
            }

            var settings = Settings(reachable, reason, reachable ? loop.ReadSubstrateToken() : null);
            var additionalDirectories = AdditionalDirectoriesOf(scope.SessionId);

            // What the head's services add to the plan. Each one degrades to a stage notice: a missing file or a failed start never stops the session.
            var notices = new List<StageEvent>();
            runModes = await OpenRunModeAsync(scope, notices, cancellationToken).ConfigureAwait(false);
            hooks = await StartHooksAsync(slot, scope, additionalDirectories, extras, notices, cancellationToken).ConfigureAwait(false);
            await RefreshMcpAsync(scope, additionalDirectories, extras, notices, cancellationToken).ConfigureAwait(false);

            var hookBridge = extras.IsRemote ? null : hooks?.Endpoint;
            (var launch, overlay) = PlanLaunch(settings, plugins, scope, additionalDirectories, hookBridge, runModes, notices);
            var plan = AssemblePlan(slot, scope, launch, plugins, settings, notices, gateway, reachable, client, extras, hooks, runModes, overlay);
            client = null;
            hooks = null;
            runModes = null;
            overlay = null;
            return plan;
        }
        finally
        {
            client?.Dispose();
            overlay?.Dispose();
            runModes?.Dispose();
            if (hooks is not null)
            {
                await hooks.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private string[]? AdditionalDirectoriesOf(string sessionId)
    {
        lock (_gate)
        {
            _ = _additionalDirectories.TryGetValue(sessionId, out var kept);
            return kept;
        }
    }

    /// <summary>Plans the launch of omp with the run mode's arguments and, when the mode has shell deny rules, writes the overlay settings file. A file that cannot be written re-plans without the rules and says so.</summary>
    private (OmpLaunchPlan Launch, ConfigOverlayWriter? Overlay) PlanLaunch(
        AgentLoopSettings settings,
        IReadOnlyList<ResolvedPlugin> plugins,
        StageObservationScope scope,
        IReadOnlyList<string>? additionalDirectories,
        HookBridgeEndpoint? hookBridge,
        RunModePolicyHolder? runModes,
        List<StageEvent> notices)
    {
        var clock = _options.TimeProvider;
        var bashOverlay = runModes?.BashOverlayYaml;
        var launch = OmpLaunchPlanner.Plan(settings, plugins, scope.SessionId, scope.WorkspaceRoot, scope.GraphId, clock, additionalDirectories, hookBridge, runModes?.Launch, bashOverlay);
        if (launch.ConfigOverlayPath is null)
        {
            return (launch, null);
        }

        try
        {
            return (launch, ConfigOverlayWriter.Write(settings, scope.SessionStateDirectory, additionalDirectories, bashOverlay));
        }
        catch (Exception exception) when (bashOverlay is not null && exception is IOException or UnauthorizedAccessException)
        {
            // The deny rules for shell commands could not reach omp: plan again without them and say so, so the session still starts.
            notices.Add(Notice(scope, "run-mode-overlay", "Run mode", StageStatus.Degraded, "The settings file with the shell deny rules could not be written, so omp does not enforce them itself in this session. " + exception.GetType().Name));
            launch = OmpLaunchPlanner.Plan(settings, plugins, scope.SessionId, scope.WorkspaceRoot, scope.GraphId, clock, additionalDirectories, hookBridge, runModes?.Launch, null);
            return (launch, launch.ConfigOverlayPath is null ? null : ConfigOverlayWriter.Write(settings, scope.SessionStateDirectory, additionalDirectories));
        }
    }

    /// <summary>Merges the stage sources of the session, its task context and its steering recorder into the plan. The caller keeps the ownership of every disposable it passes in until this returns.</summary>
    private SessionPlan AssemblePlan(
        SessionSlot slot,
        StageObservationScope scope,
        OmpLaunchPlan launch,
        IReadOnlyList<ResolvedPlugin> plugins,
        AgentLoopSettings settings,
        List<StageEvent> notices,
        SubstrateGateway? gateway,
        bool reachable,
        HttpClient? client,
        SessionExtras extras,
        IHooksSession? hooks,
        RunModePolicyHolder? runModes,
        ConfigOverlayWriter? overlay)
    {
        var events = new List<StageEvent>(launch.PlanningEvents);
        events.AddRange(notices);
        if (_options.Loop.SubstrateProblem is { } problem)
        {
            events.Add(SubstrateProblemEvent(scope, problem));
        }

        var substrateActive = gateway is not null && reachable;
        var recheck = new RecheckStageSource();
        var stages = new GraphTapStageSource(
            new CompositeStageSource(StageChildren(slot, launch, plugins, settings, events, gateway, substrateActive, recheck), _options.TimeProvider),
            slot.OnGraphId);
        ITaskContextSource context = new ScopeFollowingTaskContextSource(
            new GsdTaskContextSource(_gsd, substrateActive ? new SubstrateContextReader(gateway!, _logger) : null),
            slot.Scope);
        var steering = substrateActive
            ? new SteeringRecorderAdapter(new SubstrateSteeringRecorder(gateway!, _logger), slot.Scope)
            : null;

        return new SessionPlan(launch, stages, context, steering, recheck, gateway, substrateActive, client)
        {
            Hooks = hooks,
            RunModes = runModes,
            Overlay = overlay,
            Extras = extras,
            SessionId = scope.SessionId,
        };
    }

    /// <summary>
    /// Composes the engine of a session from its plan: the plan's launch, the MCP servers of the user's own files (none for a session on an ssh host), the hook gate, the run mode and, for a
    /// session on an ssh host, the remote resources made here. A session without services is composed exactly as it always was.
    /// </summary>
    /// <param name="plan">The plan of the session.</param>
    /// <param name="resumeSessionId">The omp session the first prompt resumes, or null to create a new one.</param>
    /// <param name="cancellationToken">Cancels the connection to an ssh host.</param>
    /// <returns>The engine. It starts no process until a prompt is submitted.</returns>
    /// <exception cref="InvalidOperationException">The ssh environment of the session is not among the saved environments.</exception>
    internal async Task<EngineComposition> ComposeEngineAsync(SessionPlan plan, string? resumeSessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var launch = plan.Launch;
        IEnvironmentSessionResources? remote = null;
        IReadOnlyList<AcpMcpServer> extraServers = [];
        if (plan.Extras.IsRemote)
        {
            remote = await CreateRemoteResourcesAsync(plan, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // The provider keys join the environment here, after the planner, so the plan the planner returned never holds a key and its own
            // Environment stays as planned. The overlay reads the keys when omp is started, so a key saved after this point still reaches it.
            if (_options.ProviderKeys is { } providerKeys)
            {
                launch = launch with { Environment = providerKeys.Overlay(launch.Environment) };
            }

            if (_services is { } services)
            {
                extraServers = services.Mcp.OfferedServers();
            }
        }

        try
        {
            var holder = plan.RunModes;
            var composition = new EngineCompositionOptions
            {
                ExtraMcpServers = extraServers,
                HookGate = plan.Hooks?.Gate,
                RunModePolicy = holder is null ? null : () => holder.Current,
                RunModeAudit = _services?.RunModeHub.Audit,
                Remote = remote,
                Clock = _options.TimeProvider,
            };
            return EngineComposition.Compose(_options.AllowedKinds, launch, _options.TransportFactory, _options.LoggerFactory, _options.OmpPath, resumeSessionId, composition);
        }
        catch
        {
            remote?.Dispose();
            throw;
        }
    }

    /// <summary>Gets or sets the rail model over this set's registry. The head sets it once it has built the model; the @-mention of another chat lists the chats through it.</summary>
    internal OmpOS.Presentation.AgentPanel.SessionCatalogModel? CatalogModel { get; set; }

    /// <summary>Returns the workspace root of a session, or the head's workspace when the session is unknown.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The absolute path.</returns>
    internal string WorkspaceRootOf(string sessionId) =>
        SlotOf(sessionId)?.Scope.Current.WorkspaceRoot ?? _options.Loop.WorkspaceRoot;

    /// <summary>Returns whether the user's run mode is Run Everything. A head without services has no run mode: false.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True for Run Everything.</returns>
    internal async Task<bool> IsRunEverythingAsync(CancellationToken cancellationToken) =>
        _services is { } services && await services.RunModeHub.LoadModeAsync(cancellationToken).ConfigureAwait(false) == RunMode.RunEverything;

    /// <summary>
    /// Composes the engine of a side chat: a plain omp in the parent's workspace that asks before every tool call (<c>--approval-mode always-ask</c>) under an approval policy that admits no tool kind, so
    /// every call is denied before anyone is asked.
    /// </summary>
    /// <param name="workspaceRoot">The workspace of the parent session.</param>
    /// <returns>The engine. It starts no process until a prompt is submitted.</returns>
    internal EngineComposition ComposeSideChatEngine(string workspaceRoot) =>
        EngineComposition.Compose([], ChildPlan(workspaceRoot, RunMode.Allowlist), _options.TransportFactory, _options.LoggerFactory, _options.OmpPath);

    /// <summary>Composes the engine of an unattended candidate (best of n): omp's <c>yolo</c> approval mode in the candidate's worktree. The caller makes sure the user chose Run Everything.</summary>
    /// <param name="workspaceRoot">The candidate's worktree.</param>
    /// <returns>The engine. It starts no process until a prompt is submitted.</returns>
    internal EngineComposition ComposeUnattendedEngine(string workspaceRoot) =>
        EngineComposition.Compose(RunModeLaunchMapper.AcpToolKinds, ChildPlan(workspaceRoot, RunMode.RunEverything), _options.TransportFactory, _options.LoggerFactory, _options.OmpPath);

    /// <summary>Adds the registry session of a fork: it resumes the session the engine created and keeps the workspace and the environment of its parent.</summary>
    /// <param name="parentSessionId">The session that was forked.</param>
    /// <param name="nativeSessionId">The identifier the engine gave the forked session.</param>
    /// <param name="cancellationToken">Cancels the call before it changes anything.</param>
    /// <returns>The new session and its title.</returns>
    internal async Task<ForkResult> AddForkedSessionAsync(string parentSessionId, string nativeSessionId, CancellationToken cancellationToken)
    {
        var registry = await EnsureRegistryAsync(cancellationToken).ConfigureAwait(false);
        var parent = registry.TryGet(parentSessionId, out var found) ? found : null;
        var title = parent is null ? "Fork" : parent.Title + " (fork)";
        var id = PrepareSession(registry, registry.Create(title, parent?.WorkspaceRoot ?? _options.Loop.WorkspaceRoot));
        registry.SetAcpSession(id, nativeSessionId, nativeSessionId);
        _extras.Set(id, _extras.Get(parentSessionId));
        return new ForkResult(id, title);
    }

    private OmpLaunchPlan ChildPlan(string workspaceRoot, RunMode mode)
    {
        IReadOnlyDictionary<string, string> environment = new Dictionary<string, string>(StringComparer.Ordinal);
        if (_options.ProviderKeys is { } providerKeys)
        {
            environment = providerKeys.Overlay(environment);
        }

        return new OmpLaunchPlan
        {
            ExtraArguments = ["--no-extensions", .. RunModeLaunchMapper.Map(mode, readsNeedGate: false).Arguments],
            Environment = environment,
            McpServers = [],
            WorkingDirectory = workspaceRoot,
            PlanningEvents = [],
        };
    }

    private (string WorkspaceRoot, SessionExtras Extras) TakePendingTarget()
    {
        var pending = _services?.NewSessionTarget;
        var target = pending?.Take();
        var roots = pending?.TakeRoots();
        var primary = roots is { Count: > 0 } ? roots[0].Path : null;
        IReadOnlyList<string> additional = roots is { Count: > 1 } ? [.. roots.Skip(1).Select(static root => root.Path)] : [];
        var environmentId = target is { Kind: SessionTargetKind.Ssh or SessionTargetKind.Container or SessionTargetKind.Wsl } ? target.ProfileId : null;
        var workspaceRoot = environmentId is null
            ? primary ?? _options.Loop.WorkspaceRoot
            : target!.RemoteWorkspaceRoot ?? primary ?? _options.Loop.WorkspaceRoot;
        return (workspaceRoot, environmentId is null && additional.Count == 0 ? SessionExtras.Local : new SessionExtras(environmentId, additional));
    }

    private async Task RememberExtrasAsync(string sessionId, SessionExtras extras, CancellationToken cancellationToken)
    {
        _extras.Set(sessionId, extras);
        lock (_gate)
        {
            if (extras.AdditionalDirectories.Count > 0)
            {
                _additionalDirectories[sessionId] = [.. extras.AdditionalDirectories];
            }
        }

        if (extras.EnvironmentId is { } profileId && _services is { } services)
        {
            try
            {
                _ = await services.RemoteProfiles.RecordUsedAsync(profileId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The last-used time only orders the environment picker.
            }
        }
    }

    private async Task<RunModePolicyHolder?> OpenRunModeAsync(StageObservationScope scope, List<StageEvent> notices, CancellationToken cancellationToken)
    {
        if (_services is not { } services)
        {
            return null;
        }

        var holder = services.RunModeHub.Open(scope.WorkspaceRoot);
        try
        {
            await holder.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            notices.Add(Notice(scope, "run-mode", "Run mode", StageStatus.Degraded, "The run mode could not be read, so omp asks before it acts. " + exception.GetType().Name));
        }

        foreach (var message in holder.Notices)
        {
            notices.Add(Notice(scope, "run-mode", "Run mode", StageStatus.Degraded, message));
        }

        return holder;
    }

    private async Task<IHooksSession?> StartHooksAsync(
        SessionSlot slot,
        StageObservationScope scope,
        IReadOnlyList<string>? additionalDirectories,
        SessionExtras extras,
        List<StageEvent> notices,
        CancellationToken cancellationToken)
    {
        if (_services is not { } services)
        {
            return null;
        }

        try
        {
            var session = await services.Hooks.StartSessionAsync(
                new HooksSessionRequest
                {
                    SessionId = scope.SessionId,
                    WorkspaceRoots = [scope.WorkspaceRoot, .. additionalDirectories ?? []],
                    IsRemote = extras.IsRemote,
                    Files = extras.IsRemote ? new NoHookFiles() : null,
                },
                cancellationToken).ConfigureAwait(false);
            session.NoticeRaised += (_, e) => slot.LiveEvents.NotifyHookNotice(e.Notice);
            if (extras.IsRemote && session.IsActive)
            {
                notices.Add(Notice(scope, "hooks", "Hooks", StageStatus.Degraded, "The hook files of the remote host are not read in this version: only hooks from plugins and the events omp reports apply."));
            }

            return session;
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or OutOfMemoryException))
        {
            notices.Add(Notice(scope, "hooks", "Hooks", StageStatus.Degraded, "Hooks could not be started, so this session runs without them. " + exception.GetType().Name));
            return null;
        }
    }

    private async Task RefreshMcpAsync(StageObservationScope scope, IReadOnlyList<string>? additionalDirectories, SessionExtras extras, List<StageEvent> notices, CancellationToken cancellationToken)
    {
        if (_services is not { } services || extras.IsRemote)
        {
            return;
        }

        try
        {
            _ = await services.Mcp.RefreshAsync(services.McpContext([scope.WorkspaceRoot, .. additionalDirectories ?? []]), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            notices.Add(Notice(scope, "mcp", "MCP servers", StageStatus.Degraded, "The MCP configuration files could not be read, so this session starts without those servers. " + exception.GetType().Name));
        }
    }

    private StageEvent Notice(StageObservationScope scope, string stageId, string title, StageStatus status, string detail) => new()
    {
        SessionId = scope.SessionId,
        Source = StageSource.Omp,
        StageId = stageId,
        Title = title,
        Status = status,
        Timestamp = Now,
        Detail = detail,
        GraphId = scope.GraphId,
    };

    private async Task<IEnvironmentSessionResources> CreateRemoteResourcesAsync(SessionPlan plan, CancellationToken cancellationToken)
    {
        var services = _services ?? throw new InvalidOperationException("This head has no saved environments.");
        var listing = await services.RemoteProfiles.ListAsync(cancellationToken).ConfigureAwait(false);
        var profile = listing.Profiles.FirstOrDefault(stored => string.Equals(stored.Profile.Id, plan.Extras.EnvironmentId, StringComparison.Ordinal))?.Profile
            ?? throw new InvalidOperationException("The environment of this session is not among your saved environments. Add it again on the Remote page.");
        IReadOnlyDictionary<string, string>? secrets = _options.ProviderKeys is { Current.Count: > 0 } keys
            ? new Dictionary<string, string>(keys.Current, StringComparer.Ordinal)
            : null;
        var launchOptions = new RemoteLaunchOptions { SessionId = plan.SessionId, RemoteHomeDirectory = "/", LocalStateRoot = _options.Loop.StateRoot };

        // The provider keys travel on the ssh, podman or wsl.exe stdin and never on a command line; the plan itself holds none.
        switch (profile)
        {
            case { Kind: EnvironmentKind.Ssh, Ssh: { } ssh }:
                return await RemoteSessionResources.CreateAsync(ssh, plan.Launch, launchOptions, secrets, services.SshOptions, _options.LoggerFactory, cancellationToken).ConfigureAwait(false);
            case { Kind: EnvironmentKind.Container, Container: { } container }:
                return await ContainerSessionResources.CreateAsync(container, plan.Launch, launchOptions, secrets, services.PodmanOptions, _options.LoggerFactory, cancellationToken).ConfigureAwait(false);
            case { Kind: EnvironmentKind.Wsl, Wsl: { } wsl }:
                if (!services.ContainerEnvironments.SupportsWsl)
                {
                    throw new InvalidOperationException(ContainerCopy.WslWindowsOnly);
                }

                return await WslSessionResources.CreateAsync(wsl, plan.Launch, launchOptions, secrets, services.WslOptions, _options.LoggerFactory, cancellationToken).ConfigureAwait(false);
            default:
                throw new InvalidOperationException("The environment of this session has no details a session can start in. Edit it on the Remote page.");
        }
    }

    private ISlashDiscoverySource SlashDiscoveryFor(AgentRow row, SessionSlot slot) =>
        _extras.Get(row.Id).IsRemote
            ? new RemoteSlashDiscoverySource(slot)
            : new ConventionSlashDiscoverySource(
                [row.Summary.WorkspaceRoot ?? _options.Loop.WorkspaceRoot],
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private Func<IReadOnlyList<IMentionSource>>? MentionSourcesFor(AgentRow row)
    {
        if (CatalogModel is not { } catalog)
        {
            return null;
        }

        var root = row.Summary.WorkspaceRoot ?? _options.Loop.WorkspaceRoot;
        var extras = _extras.Get(row.Id);
        return () =>
        {
            IReadOnlyList<string> patterns = [];
            var useDefault = true;
            var hierarchical = false;
            try
            {
                var ignore = new FileSettingsStore(_options.Loop.StateRoot).ReadIgnore();
                patterns = ignore.GlobalPatterns;
                useDefault = ignore.UseDefaultList;
                hierarchical = ignore.HierarchicalIgnoreFiles;
            }
            catch (SettingsSourceException)
            {
                // An unreadable settings file leaves the default ignore list; the settings page reports the file.
            }

            return MentionSourceFactory.Create(new MentionSourceContext
            {
                SessionId = row.Id,
                WorkspaceRoot = root,
                AdditionalRoots = extras.AdditionalDirectories,
                Catalog = catalog,
                ReadTranscript = ReadTranscriptAsync,
                GlobalIgnorePatterns = patterns,
                UseDefaultIgnoreList = useDefault,
                HierarchicalIgnore = hierarchical,
                Files = extras.IsRemote ? new RemoteMentionFiles() : null,
                TimeProvider = _options.TimeProvider,
            });
        };
    }

    private Task<MentionContent> ReadTranscriptAsync(string sessionId, int maxBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var text = SlotOf(sessionId)?.Runtime?.TranscriptText();
        return string.IsNullOrWhiteSpace(text)
            ? throw new MentionUnavailableException("This chat has no messages that can be attached yet.")
            : Task.FromResult(MentionTextCut.Cut(text, maxBytes));
    }

    /// <summary>Describes what a re-check of one plugin found.</summary>
    /// <param name="plugin">The plugin as it resolves now.</param>
    /// <param name="plan">The plan of the session being checked.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The stage status and its plain-text detail.</returns>
    internal async Task<(StageStatus Status, string Detail)> DescribeRecheckAsync(ResolvedPlugin plugin, SessionPlan plan, CancellationToken cancellationToken)
    {
        if (plugin.Health != PluginHealth.Ready)
        {
            var status = plugin.Health switch
            {
                PluginHealth.Disabled => StageStatus.Skipped,
                PluginHealth.EntryMissing or PluginHealth.ManifestInvalid => StageStatus.Degraded,
                _ => StageStatus.Unavailable,
            };
            return (status, plugin.Message);
        }

        if (plugin.Descriptor.Role == OmpPluginRole.SessionSubstrate && plan.Gateway is { } gateway)
        {
            var (reachable, reason) = await CheckHealthAsync(gateway, cancellationToken).ConfigureAwait(false);
            return reachable
                ? (StageStatus.Succeeded, $"{plugin.Message} The Substrate answers. It is used the next time this agent starts.")
                : (StageStatus.Degraded, $"{reason} The Substrate is left out of this agent until it starts again.");
        }

        return (StageStatus.Succeeded, $"{plugin.Message} It loads the next time this agent starts.");
    }

    /// <summary>
    /// Refreshes the heartbeat of every session whose turn is running, then marks every running session that went silent as lost. The
    /// timer calls it; a test calls it directly.
    /// </summary>
    internal void RunMaintenance()
    {
        var registry = Registry;
        if (registry is null)
        {
            return;
        }

        try
        {
            List<SessionSlot> active;
            lock (_gate)
            {
                active = [.. _slots.Values.Where(static slot => slot.TurnActive)];
            }

            foreach (var slot in active)
            {
                registry.Touch(slot.Id);
            }

            registry.DetectLost();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // The maintenance timer must never fault the process; the next tick tries again.
            if (_logger is not null)
            {
                MaintenanceFailed(_logger, exception.GetType().Name, null);
            }
        }
    }

    /// <summary>Stops every engine, disposes every runtime and releases the registry and the timer. Lifecycles are left as they are, so a turn that was running is lost and can be resumed.</summary>
    /// <returns>A task that completes when every engine is stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        List<SessionSlot> slots;
        ITimer? timer;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            slots = [.. _slots.Values];
            _slots.Clear();
            timer = _timer;
            _timer = null;
        }

        if (timer is not null)
        {
            await timer.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var slot in slots)
        {
            await DisposeSlotAsync(slot).ConfigureAwait(false);
        }

        await _sideChats.DisposeAsync().ConfigureAwait(false);
        _catalog.Detach();
        _reloadGate.Dispose();
    }

    /// <summary>
    /// Creates the private directories every omp process of a session needs before it starts: the session directory, the steering inbox,
    /// acknowledgements and done directories, the connection report directory and the Swarm directory. Only their owner may enter them.
    /// </summary>
    /// <param name="sessionDirectory">The directory returned by <see cref="SessionStateLayout.SessionDirectory"/>.</param>
    internal static void EnsureSessionLayout(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);

        // The state of a session may hold plans, notes and tokens: only its owner may enter the directory.
        EnsurePrivateDirectory(Path.GetDirectoryName(sessionDirectory)!);
        EnsurePrivateDirectory(sessionDirectory);
        var steer = SessionStateLayout.SteerDirectory(sessionDirectory);
        EnsurePrivateDirectory(steer);
        foreach (var name in SteerSubdirectories)
        {
            EnsurePrivateDirectory(Path.Combine(steer, name));
        }

        EnsurePrivateDirectory(Path.GetDirectoryName(SessionStateLayout.ConnectionFile(sessionDirectory))!);
        EnsurePrivateDirectory(SessionStateLayout.SwarmDirectory(sessionDirectory));
    }

    /// <summary>Creates a directory that only its owner may enter. An existing directory is left as it is.</summary>
    /// <param name="path">The directory.</param>
    internal static void EnsurePrivateDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Directory.Exists(path))
        {
            return;
        }

        Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows() && new DirectoryInfo(path).LinkTarget is null)
        {
            File.SetUnixFileMode(path, OwnerOnly);
        }
    }

    private StageEvent SubstrateProblemEvent(StageObservationScope scope, string problem) => new()
    {
        SessionId = scope.SessionId,
        Source = StageSource.Substrate,
        StageId = "plugin",
        Title = "Agent Substrate plugin",
        Status = StageStatus.Unavailable,
        Timestamp = Now,
        Detail = problem,
        GraphId = scope.GraphId,
    };

    private async Task ReloadCoreAsync(bool onlyWhenUnloaded, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _reloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var first = Registry is null;
            if (onlyWhenUnloaded && !first)
            {
                return;
            }

            var plugins = await Task.Run(ResolvePlugins, cancellationToken).ConfigureAwait(false);
            EnsurePrivateDirectory(_options.Loop.StateRoot);
            var registry = await AgentSessionRegistry.OpenAsync(
                Path.Combine(_options.Loop.StateRoot, RegistryFileName),
                _options.TimeProvider,
                _logger,
                new AgentSessionRegistryOptions { StateRoot = _options.Loop.StateRoot },
                cancellationToken).ConfigureAwait(false);
            if (first)
            {
                // No engine is attached to any session yet, so whatever the last run left Running is lost.
                registry.RecoverAfterRestart();
            }

            lock (_gate)
            {
                _plugins = plugins;
                _registry = registry;
                if (first)
                {
                    foreach (var record in registry.SnapshotRecords().Where(static record => record.Lifecycle == SessionLifecycle.Paused))
                    {
                        _closed.Add(record.Id);
                    }
                }
            }

            _catalog.Attach(registry);
            if (first)
            {
                StartMaintenance();
            }
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    private async Task<AgentSessionRegistry> EnsureRegistryAsync(CancellationToken cancellationToken)
    {
        if (Registry is { } loaded)
        {
            return loaded;
        }

        await ReloadCoreAsync(onlyWhenUnloaded: true, cancellationToken).ConfigureAwait(false);
        return Registry ?? throw new InvalidOperationException("The agent registry could not be opened.");
    }

    private void StartMaintenance()
    {
        var interval = _options.MaintenanceInterval;
        if (interval <= TimeSpan.Zero || interval == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed || _timer is not null)
            {
                return;
            }

            _timer = _options.TimeProvider.CreateTimer(static state => ((SessionEngineSet)state!).RunMaintenance(), this, interval, interval);
        }
    }

    private IReadOnlyList<ResolvedPlugin> ResolvePlugins() => Settings(reachable: true, reason: null, token: null).ResolveBuiltInPlugins();

    private AgentLoopSettings Settings(bool reachable, string? reason, string? token)
    {
        var loop = _options.Loop;
        return new AgentLoopSettings
        {
            PluginsRoot = loop.PluginsRoot,
            StateRoot = loop.StateRoot,
            SubstrateEnabled = loop.Substrate is not null || loop.SubstrateProblem is not null,
            SubstrateEndpoint = loop.Substrate?.BaseUri,
            SubstrateToken = token,
            SubstrateTokensDirectory = loop.SubstrateTokensDirectory,
            SubstrateReachable = reachable,
            SubstrateUnreachableReason = reason,
        };
    }

    private HttpClient CreateSubstrateClient() =>
        _options.SubstrateHandler is { } handler
            ? new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) }
            : SubstrateGateway.CreateHttpClient();

    private async Task<(bool Reachable, string? Reason)> CheckHealthAsync(SubstrateGateway gateway, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(_options.HealthTimeout, _options.TimeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            await gateway.HealthAsync(linked.Token).ConfigureAwait(false);
            return (true, null);
        }
        catch (SubstrateUnavailableException exception)
        {
            // The text reaches the pipeline flyout: it names the HTTP status when there was one, never the internal reason code.
            var status = exception.StatusCode is { } code ? $" (HTTP {code.ToString(CultureInfo.InvariantCulture)})" : string.Empty;
            return (false, $"The Substrate did not answer its health check{status}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, "The Substrate did not answer its health check in time.");
        }
    }

    private List<NamedStageSource> StageChildren(
        SessionSlot slot,
        OmpLaunchPlan launch,
        IReadOnlyList<ResolvedPlugin> plugins,
        AgentLoopSettings settings,
        IReadOnlyList<StageEvent> planningEvents,
        SubstrateGateway? gateway,
        bool substrateActive,
        RecheckStageSource recheck)
    {
        var clock = _options.TimeProvider;
        var factory = _options.LoggerFactory;
        var planner = plugins.FirstOrDefault(static plugin => string.Equals(plugin.Descriptor.Id, OmpPluginCatalog.UltrathinkId, StringComparison.Ordinal));
        var planningReady = planner is { Health: PluginHealth.Ready };
        var children = new List<NamedStageSource>
        {
            new("Plugin availability", StageSource.Omp, new PluginAvailabilityStageSource(plugins, settings, clock), MayComplete: true),
            new("Launch plan", StageSource.Omp, new PlanningEventsStageSource(planningEvents), MayComplete: true),
            new(
                "Plugin connection",
                StageSource.Omp,
                new PluginConnectionStageSource(PluginConnectionStageSourceOptions.ForPlan(launch), clock, factory?.CreateLogger<PluginConnectionStageSource>())),
            new(
                "Ultrathink",
                StageSource.Ultrathink,
                new ScopeFollowingStageSource(
                    new UltrathinkStateObserver(new UltrathinkStateObserverOptions { PlanningEnabled = planningReady }, clock, factory?.CreateLogger<UltrathinkStateObserver>()),
                    slot.Scope)),
            new("Agent Swarm", StageSource.Swarm, new SwarmLiveObserver(null, clock, factory?.CreateLogger<SwarmLiveObserver>())),
        };
        if (substrateActive)
        {
            children.Add(new NamedStageSource("Substrate", StageSource.Substrate, new ScopeFollowingStageSource(new SubstrateStageObserver(gateway!, clock), slot.Scope)));
        }

        children.Add(new NamedStageSource("GSD", StageSource.Gsd, new GsdStateObserver(_gsd, clock)));
        children.Add(new NamedStageSource("Re-check", StageSource.Omp, recheck));
        return children;
    }

    private StageObservationScope BuildScope(string sessionId, string? workspaceRoot, string? graphId)
    {
        var record = _registry is { } registry && registry.TryGet(sessionId, out var found) ? found : null;
        return new StageObservationScope
        {
            SessionId = sessionId,
            WorkspaceRoot = record?.WorkspaceRoot ?? workspaceRoot ?? _options.Loop.WorkspaceRoot,
            SessionStateDirectory = record?.StateDirectory ?? SessionStateLayout.SessionDirectory(_options.Loop.StateRoot, sessionId),
            AcpSessionId = record?.AcpSessionId,
            GraphId = record?.GraphId ?? graphId,
        };
    }

    /// <summary>Attaches an engine to a lost or closed session again: the next prompt composes the engine, resuming the stored omp session, and the registry shows the session as ready.</summary>
    /// <param name="slot">The slot of the session.</param>
    /// <param name="cancellationToken">Cancels the call before it changes anything.</param>
    /// <returns>A completed task.</returns>
    internal async Task ReattachAsync(SessionSlot slot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        slot.ReopenEngine();
        lock (_gate)
        {
            _closed.Remove(slot.Id);
        }

        var registry = Registry;
        var freshTerminal = registry is not null && registry.TryGet(slot.Id, out var record) && record.Kind == SessionKind.Terminal && record.ResumeReference is null;
        registry?.SetLifecycle(slot.Id, SessionLifecycle.Created);
        if (freshTerminal)
        {
            await PublishFreshTerminalNoticeAsync(slot, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Mirrors what the omp of a terminal session does into the registry: running is Running, an exit with code 0 is Completed, any other
    /// exit and an omp that could not be started are Failed. A terminal that was stopped because its session was closed leaves the
    /// lifecycle to the close.
    /// </summary>
    /// <param name="slot">The slot of the terminal session.</param>
    /// <param name="terminal">The terminal model, in the state that changed.</param>
    internal void OnTerminalChanged(SessionSlot slot, OmpTerminalViewModel terminal)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(terminal);
        var registry = Registry;
        if (registry is null || slot.EngineClosed)
        {
            return;
        }

        switch (terminal.State)
        {
            case OmpTerminalState.Running:
                slot.SetTurnActive(true);
                registry.SetLifecycle(slot.Id, SessionLifecycle.Running);
                break;
            case OmpTerminalState.Exited:
                slot.SetTurnActive(false);
                registry.SetLifecycle(slot.Id, terminal.ExitCode == 0 ? SessionLifecycle.Completed : SessionLifecycle.Failed);
                break;
            case OmpTerminalState.Unavailable:
                slot.SetTurnActive(false);
                registry.SetLifecycle(slot.Id, SessionLifecycle.Failed);
                break;
            case OmpTerminalState.Idle:
                slot.SetTurnActive(false);
                return;
            default:
                return;
        }

        registry.Touch(slot.Id);
    }

    private async Task PublishFreshTerminalNoticeAsync(SessionSlot slot, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await slot.PlanAsync(cancellationToken).ConfigureAwait(false);
            plan.Recheck.Publish(new StageEvent
            {
                SessionId = slot.Id,
                Source = StageSource.Omp,
                StageId = "terminal-resume",
                Title = "omp terminal",
                Status = StageStatus.Degraded,
                Timestamp = Now,
                Detail = "omp does not report the session of a terminal, so this terminal starts a new omp session. The earlier conversation is not resumed.",
                GraphId = slot.Scope.Current.GraphId,
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            // The notice is informational: a plan that cannot be built is reported by the session's own stages.
            if (_logger is not null)
            {
                TerminalNoticeFailed(_logger, exception.GetType().Name, null);
            }
        }
    }

    /// <summary>Mirrors what a runtime reports about its run into the registry: started is Running, completed is Completed (Created when the operator stopped the run), faulted is Failed.</summary>
    /// <param name="slot">The slot of the session whose run changed.</param>
    /// <param name="e">What happened to the run.</param>
    internal void OnTurnChanged(SessionSlot slot, SessionTurnEventArgs e)
    {
        var registry = Registry;
        if (registry is null || slot.EngineClosed)
        {
            // A turn that ends because its engine was closed must not overwrite the Paused or Completed the close recorded.
            return;
        }

        switch (e.Kind)
        {
            case SessionTurnKind.Started:
                slot.SetTurnActive(true);
                registry.SetLifecycle(slot.Id, SessionLifecycle.Running);
                break;
            case SessionTurnKind.Completed:
                slot.SetTurnActive(false);
                registry.SetLifecycle(slot.Id, string.Equals(e.Reason, CancelledReason, StringComparison.Ordinal) ? SessionLifecycle.Created : SessionLifecycle.Completed);
                break;
            case SessionTurnKind.Faulted:
                slot.SetTurnActive(false);
                registry.SetLifecycle(slot.Id, SessionLifecycle.Failed);
                break;
            default:
                return;
        }

        registry.Touch(slot.Id);
    }

    private async Task DisposeSlotAsync(SessionSlot slot)
    {
        try
        {
            if (slot.Runtime is { } runtime)
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }

            await slot.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // One session that fails to stop must not leave the others running.
            if (_logger is not null)
            {
                DisposeFailed(_logger, exception.GetType().Name, null);
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Gsd;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Plugins;
using OmpOS.Harness.AgentLoop.Sessions;
using OmpOS.Harness.AgentLoop.Stages;
using OmpOS.Harness.AgentLoop.Substrate;
using OmpOS.Native.Engine;
using OmpOS.Native.Views.Sessions;
using OmpOS.Presentation.AgentPanel;

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

    private static readonly Action<ILogger, string, Exception?> MaintenanceFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(1, "AgentMaintenanceFailed"),
        "The agent maintenance tick failed ({ExceptionType}); the next tick tries again.");

    private static readonly Action<ILogger, string, Exception?> DisposeFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(2, "AgentSessionDisposeFailed"),
        "Disposing an agent session failed ({ExceptionType}); the other sessions are still stopped.");

    private readonly SessionEngineSetOptions _options;
    private readonly ReloadableSessionCatalog _catalog = new();
    private readonly System.Threading.Lock _gate = new();
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly Dictionary<string, SessionSlot> _slots = new(StringComparer.Ordinal);
    private readonly HashSet<string> _closed = new(StringComparer.Ordinal);
    private readonly GsdToolsClient _gsd;
    private readonly ILogger? _logger;
    private AgentSessionRegistry? _registry;
    private IReadOnlyList<ResolvedPlugin> _plugins = [];
    private ITimer? _timer;
    private bool _disposed;

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
        var record = registry.Create(title: null, _options.Loop.WorkspaceRoot);
        try
        {
            // The state of a session may hold plans, notes and tokens: only its owner may enter the directory.
            EnsurePrivateDirectory(Path.GetDirectoryName(record.StateDirectory)!);
            EnsurePrivateDirectory(record.StateDirectory);
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

        var runtime = new AgentSessionRuntime(new AgentSessionRuntimeOptions
        {
            Row = row,
            Prompts = slot.Prompts,
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
        });
        slot.Runtime = runtime;
        runtime.TurnChanged += (_, e) => OnTurnChanged(slot, e);
        if (IsClosed(row.Id))
        {
            slot.MarkEngineClosed();
            runtime.ViewModel.MarkClosed();
        }

        runtime.StartObserving();
        return runtime.ViewModel;
    }

    /// <summary>
    /// Returns the slot of a session, creating it on first use: its live scope, send path, approval bridge and stage sources. The slot
    /// starts no process and reads no file until something asks for its plan or its engine. <see cref="OpenSession"/> builds the runtime over it.
    /// </summary>
    /// <param name="sessionId">The session, which the registry knows.</param>
    /// <param name="workspaceRoot">The workspace to use when the registry has none for the session, or null for the head's workspace.</param>
    /// <param name="graphId">The Graph identifier to use when the registry has none for the session, or null.</param>
    /// <returns>The slot.</returns>
    /// <exception cref="ObjectDisposedException">The set was disposed.</exception>
    internal SessionSlot CreateSlot(string sessionId, string? workspaceRoot, string? graphId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_slots.TryGetValue(sessionId, out var known))
            {
                return known;
            }

            var slot = new SessionSlot(this, new SessionScopeProvider(BuildScope(sessionId, workspaceRoot, graphId)));
            _slots[sessionId] = slot;
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
        var scope = slot.Scope.Current;
        var plugins = Plugins;
        var clock = _options.TimeProvider;
        var loop = _options.Loop;
        HttpClient? client = null;
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
            var launch = OmpLaunchPlanner.Plan(settings, plugins, scope.SessionId, scope.WorkspaceRoot, scope.GraphId, clock);
            var events = new List<StageEvent>(launch.PlanningEvents);
            if (loop.SubstrateProblem is { } problem)
            {
                events.Add(SubstrateProblemEvent(scope, problem));
            }

            var substrateActive = gateway is not null && reachable;
            var recheck = new RecheckStageSource();
            var stages = new GraphTapStageSource(
                new CompositeStageSource(StageChildren(slot, plugins, settings, events, gateway, substrateActive, recheck), clock),
                slot.OnGraphId);
            ITaskContextSource context = new ScopeFollowingTaskContextSource(
                new GsdTaskContextSource(_gsd, substrateActive ? new SubstrateContextReader(gateway!, _logger) : null),
                slot.Scope);
            var steering = substrateActive
                ? new SteeringRecorderAdapter(new SubstrateSteeringRecorder(gateway!, _logger), slot.Scope)
                : null;

            var plan = new SessionPlan(launch, stages, context, steering, recheck, gateway, substrateActive, client);
            client = null;
            return plan;
        }
        finally
        {
            client?.Dispose();
        }
    }

    /// <summary>Composes the engine of a session from its launch plan.</summary>
    /// <param name="launch">How omp is launched for the session.</param>
    /// <param name="resumeSessionId">The omp session the first prompt resumes, or null to create a new one.</param>
    /// <returns>The engine. It starts no process until a prompt is submitted.</returns>
    internal EngineComposition ComposeEngine(OmpLaunchPlan launch, string? resumeSessionId) =>
        EngineComposition.Compose(_options.AllowedKinds, launch, _options.TransportFactory, _options.LoggerFactory, _options.OmpPath, resumeSessionId);

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

        _catalog.Detach();
        _reloadGate.Dispose();
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
            return (false, $"The Substrate did not answer its health check ({exception.Reason}).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, "The Substrate did not answer its health check in time.");
        }
    }

    private List<NamedStageSource> StageChildren(
        SessionSlot slot,
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
    internal Task ReattachAsync(SessionSlot slot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        slot.ReopenEngine();
        lock (_gate)
        {
            _closed.Remove(slot.Id);
        }

        Registry?.SetLifecycle(slot.Id, SessionLifecycle.Created);
        return Task.CompletedTask;
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

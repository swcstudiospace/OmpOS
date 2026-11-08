using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Native.Composition.Automations;
using Autonogrammer.Native.Cowork;
using Autonogrammer.Native.Logging;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Settings.Models;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Presentation.Settings.Models;
using Avalonia.Threading;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// The composition of the Agent Panel and the Kanban tab for the native head: the engines of N sessions (<see cref="SessionEngineSet"/>),
/// the rail model over their registry, the panel view model the main view shows and the Kanban board over the same sessions. The panel
/// starts <see cref="PanelState.Loading"/> and stays so until <see cref="InitializeAsync"/> has loaded the registry; a missing plugins root
/// or an uninitialised submodule never stops it, because each plugin then reports itself unavailable and the sessions run as plain omp.
/// The board takes the Substrate fields of its cards from the sessions' own task context and steers a session through the session's own
/// steering channel; the head reads those fields again on a timer.
/// </summary>
internal sealed class AgentHead : IAsyncDisposable
{
    /// <summary>How often the board reads the Substrate fields of its cards again.</summary>
    public static readonly TimeSpan DefaultDetailsRefreshInterval = TimeSpan.FromSeconds(30);

    private readonly IDisposable _loading;
    private readonly bool _selectMostRecent;
    private readonly Action<Action>? _dispatch;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _detailsInterval;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly System.Threading.Lock _gate = new();
    private ITimer? _detailsTimer;
    private Task _refreshTask = Task.CompletedTask;
    private Task? _disposeTask;
    private int _refreshing;
    private int _initialised;
    private int _disposed;

    private AgentHead(
        SessionEngineSet host,
        SessionCatalogModel catalog,
        AgentPanelViewModel panel,
        KanbanBoardModel board,
        IDisposable loading,
        bool selectMostRecent,
        Action<Action>? dispatch,
        TimeProvider clock,
        TimeSpan detailsInterval,
        HeadSettings settings)
    {
        Host = host;
        Settings = settings;
        Catalog = catalog;
        _selectMostRecent = selectMostRecent;
        Panel = panel;
        Board = board;
        _loading = loading;
        _dispatch = dispatch;
        _clock = clock;
        _detailsInterval = detailsInterval;
    }

    /// <summary>Gets the engines and the registry.</summary>
    public SessionEngineSet Host { get; }

    /// <summary>Gets the rail model over the registry.</summary>
    public SessionCatalogModel Catalog { get; }

    /// <summary>Gets the panel view model the main view shows.</summary>
    public AgentPanelViewModel Panel { get; }

    /// <summary>Gets the Kanban board over the same sessions: one card per session, its Substrate fields and its steering.</summary>
    public KanbanBoardModel Board { get; }

    /// <summary>Gets the head's side of the Settings window: the settings file, the one Settings window and the sweeper that applies the Runtime settings. The main window is connected to it with <see cref="HeadSettings.Attach"/>.</summary>
    public HeadSettings Settings { get; }

    /// <summary>Gets the shared services (hooks, remote environments, MCP catalog, checkpoints, run mode, cloud) the shell and the Settings window read, or null for a head built without them. The head disposes them.</summary>
    public Autonogrammer.Native.Composition.HeadServices? Services { get; private init; }

    /// <summary>Gets the Cowork head. Disposed before <see cref="Host"/>.</summary>
    public CoworkHead? Cowork { get; private set; }

    /// <summary>Gets the bundle composition registered for this head. Remote sessions load no bundles.</summary>
    internal Autonogrammer.Native.Composition.Plugins.BundleComposition? Bundles { get; private set; }

    /// <summary>Gets the Cowork browser composition, or null before attach.</summary>
    internal BrowserUseCompositionResult? BrowserUse { get; private set; }

    [SuppressMessage("Maintainability", "MA0051", Justification = "The acquire, transfer and cleanup steps stay in one flow so the cleanup guarantee sits beside the owners it protects.")]
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: each owner is nulled once transferred to its composed holder; the finally disposes any owner still held directly.")]
    private static async Task<AgentHead> CreateProductionCoreAsync(Autonogrammer.Native.Composition.HeadServices? services)
    {
        var loop = AgentLoopEnvironment.Resolve();
        var store = new FileSettingsStore(loop.StateRoot);

        // Keep the original browser owner until BuildAsync has retained the successful head's reference.
        BrowserUseCompositionResult? browser = null;
        ModelsSettingsComposition? models = null;
        var completed = false;
        try
        {
            browser = BrowserUseComposition.Wire(new BrowserUseCompositionRequest { StateDirectory = loop.StateRoot, Artifacts = services?.Artifacts.ToolProvider });
            // The Models page and the provider keys omp is started with are built together, so a key saved on the page reaches every omp the head starts.
            models = ModelsSettingsComposition.Create(new ModelsSettingsCompositionOptions { StateRoot = loop.StateRoot });
            var modelSource = models.Source;
            var engineOptions = new SessionEngineSetOptions
            {
                Loop = loop,
                AllowedKinds = Program.ProductionAllowedKinds,
                LoggerFactory = DesktopLogging.Factory,
                Motion = HeadSettings.ReadOrDefaults(store).Motion,
                ProviderKeys = models.Keys,
                Services = services,
            };
            var settingsOptions = new HeadSettingsOptions
            {
                ModelsPageFactory = () => new ModelsSettingsPage(modelSource),
                NetworkSource = NetworkDiagnosticsSource.Over(models.Omp.ModelsFile),
                Owned = models,
                SweepInterval = SessionLifecycleSweeper.DefaultInterval,
                Services = services,
                ComputerUse = static () => Autonogrammer.Native.Composition.ComputerUse.ComputerUseHeadComposition.SettingsFactory?.Invoke(),
                BrowserUse = browser.Settings,
            };

            // BuildAsync adopts the models at entry, including failure before settings construction.
            var build = BuildAsync(engineOptions, UiDispatch, selectMostRecent: true, DefaultDetailsRefreshInterval, store, settingsOptions, services, browser);
            models = null;
            var head = await build.ConfigureAwait(true);
            completed = true;
            return head;
        }
        finally
        {
            ExceptionDispatchInfo? failure = null;
            try
            {
                if (models is not null)
                {
                    models.Dispose();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (browser is not null)
                {
                    browser.Dispose();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            CleanupFailureReporter.Report(failure, completed, DesktopLogging.Factory.CreateLogger("Autonogrammer.Native.Agents"));
        }
    }

    /// <summary>
    /// Builds the head of the running process, and first opens the services every window shares (hooks, remote environments, MCP catalog, checkpoints, run mode,
    /// cloud agents). The services are opened asynchronously, so the application shows its window once this has completed.
    /// </summary>
    /// <param name="topLevel">Returns the window the system browser is asked through, or null.</param>
    /// <param name="cancellationToken">Cancels the opening of the services.</param>
    /// <returns>The head. It opens nothing more until <see cref="InitializeAsync"/>.</returns>
    public static async Task<AgentHead> CreateProductionAsync(Func<Avalonia.Controls.TopLevel?> topLevel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        var loop = AgentLoopEnvironment.Resolve();
        Autonogrammer.Native.Composition.HeadServices? services = null;
        try
        {
            services = await Autonogrammer.Native.Composition.HeadServices.CreateAsync(
                new Autonogrammer.Native.Composition.HeadServicesOptions
                {
                    StateRoot = loop.StateRoot,
                    PluginsRoot = loop.PluginsRoot,
                    WorkspaceRoot = loop.WorkspaceRoot,
                    OmpManagedRoot = Autonogrammer.Native.Composition.HeadServices.ResolveManagedOmpRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), loop.StateRoot),
                    TopLevel = topLevel,
                    LoggerFactory = DesktopLogging.Factory,
                },
                cancellationToken).ConfigureAwait(true);
            var head = await CreateProductionCoreAsync(services).ConfigureAwait(true);
            services = null;
            return head;
        }
        finally
        {
            if (services is not null)
            {
                await services.DisposeAsync().ConfigureAwait(true);
            }
        }
    }

    /// <summary>Builds a head over explicit roots. Tests use it with a scripted transport.</summary>
    /// <param name="options">The roots and seams. Supplied services are borrowed during construction and owned by the returned head only on success.</param>
    /// <returns>The head, after awaiting rollback of acquired resources on failure. It opens nothing until <see cref="InitializeAsync"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: each owner is nulled once transferred to its composed holder; the finally disposes any owner still held directly.")]
    public static async Task<AgentHead> CreateAsync(AgentHeadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var variables = options.GetVariable ?? (static _ => null);
        string? Lookup(string name) => name switch
        {
            AgentLoopEnvironment.PluginsRootVariable => options.PluginsRoot,
            AgentLoopEnvironment.StateDirectoryVariable => options.StateRoot,
            _ => variables(name),
        };

        var loop = AgentLoopEnvironment.Resolve(Lookup, options.WorkspaceRoot, options.WorkspaceRoot, options.WorkspaceRoot);
        BrowserUseCompositionResult? browser = null;
        try
        {
            browser = BrowserUseComposition.Wire(new BrowserUseCompositionRequest { StateDirectory = loop.StateRoot, Artifacts = options.Services?.Artifacts.ToolProvider });
            return await BuildAsync(
                new SessionEngineSetOptions
                {
                    Loop = loop,
                    AllowedKinds = options.AllowedKinds,
                    TimeProvider = options.TimeProvider ?? TimeProvider.System,
                    TransportFactory = options.TransportFactory,
                    LoggerFactory = options.LoggerFactory,
                    OmpPath = options.OmpPath,
                    Motion = options.Motion,
                    SubstrateHandler = options.SubstrateHandler,
                    MaintenanceInterval = options.MaintenanceInterval ?? SessionEngineSetOptions.DefaultMaintenanceInterval,
                    TerminalBackend = options.TerminalBackend,
                    SteerAckTimeout = options.SteerAckTimeout ?? SessionEngineSetOptions.DefaultSteerAckTimeout,
                    SteeringOptions = options.SteeringOptions,
                    LiveSteering = options.LiveSteering,
                    TerminalLocator = options.TerminalLocator,
                    ProviderKeys = options.ProviderKeys,
                    Services = options.Services,
                },
                options.Dispatch,
                options.SelectMostRecentSession,
                options.DetailsRefreshInterval ?? DefaultDetailsRefreshInterval,
                new FileSettingsStore(loop.StateRoot),
                new HeadSettingsOptions
                {
                    ModelsPageFactory = options.ModelsPageFactory,
                    GetVariable = variables,
                    SweepInterval = options.RuntimeSweepInterval,
                    Services = options.Services,
                    ComputerUse = static () => Autonogrammer.Native.Composition.ComputerUse.ComputerUseHeadComposition.SettingsFactory?.Invoke(),
                    BrowserUse = browser.Settings,
                },
                options.Services,
                browser).ConfigureAwait(true);
        }
        finally
        {
            if (browser is not null)
            {
                browser.Dispose();
            }
        }
    }

    /// <summary>
    /// Loads the plugins and the saved sessions, then selects the most recent session and ends the board's loading state. A failure shows
    /// as the panel's Error state with its retry; it does not throw. Calling it again does nothing.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the panel left the Loading state.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _initialised, 1) != 0)
        {
            return;
        }

        try
        {
            await Host.ReloadAsync(cancellationToken).ConfigureAwait(true);
            Catalog.Refresh();
            Catalog.StartRefreshing();
            if (_selectMostRecent && Catalog.Sessions.Count > 0 && Catalog.SelectedId is null)
            {
                Panel.SelectSession(Catalog.Sessions[0].Id);
                await Panel.SelectionLoad.WaitAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            // The application is closing; there is nothing to show.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Panel.ReportLoadFailure(exception.Message);
        }
        finally
        {
            _loading.Dispose();
            Board.CompleteLoading();
            StartDetailsRefresh();
            Settings.Start();

            // The head is open: the schedules of the automations start to count.
            Services?.Automations.Start();
        }
    }

    /// <summary>Stops every engine, then releases the panel, the board and the rail model. The registry keeps what it holds, so a running turn is found lost at the next start.</summary>
    /// <returns>A task that completes when every engine is stopped.</returns>
    public ValueTask DisposeAsync()
    {
        // A repeated call awaits the first teardown, so its task completes only after every engine is stopped.
        lock (_gate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposed, 1);
                _disposeTask = DisposeCoreAsync();
            }

            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync()
    {
        try
        {
            try
            {
                // The panel goes first: its Dispose cancels a selection that is still starting, so the wait below ends before the host that selection uses stops.
                Panel.Dispose();
                await Panel.SelectionLoad.WaitAsync(CancellationToken.None).ConfigureAwait(true);
            }
            finally
            {
                await StopRefreshingAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            try
            {
                await DisposeSettingsAndCoworkAsync().ConfigureAwait(true);
            }
            finally
            {
                try
                {
                    await DisposeHostAsync().ConfigureAwait(true);
                }
                finally
                {
                    DisposeModels();
                }
            }
        }
    }

    private async ValueTask StopRefreshingAsync()
    {
        _loading.Dispose();
        ITimer? timer;
        lock (_gate)
        {
            timer = _detailsTimer;
            _detailsTimer = null;
        }

        if (timer is not null)
        {
            await timer.DisposeAsync().ConfigureAwait(true);
        }

        await _lifetime.CancelAsync().ConfigureAwait(true);
#pragma warning disable VSTHRD003 // The refresh task is started by this head's own StartDetailsRefresh, so awaiting it cannot wait on work from another context.
        await Volatile.Read(ref _refreshTask).ConfigureAwait(true);
#pragma warning restore VSTHRD003
    }

    private async ValueTask DisposeSettingsAndCoworkAsync()
    {
        try
        {
            await Settings.DisposeAsync().ConfigureAwait(true);
        }
        finally
        {
            if (Cowork is { } cowork)
            {
                await cowork.DisposeAsync().ConfigureAwait(true);
            }
        }
    }

    private async ValueTask DisposeHostAsync()
    {
        try
        {
            // A run in flight works in a session of the host, so the automations stop before the engines.
            if (Services is { } automationServices)
            {
                await automationServices.Automations.DisposeAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            try
            {
                await Host.DisposeAsync().ConfigureAwait(true);
            }
            finally
            {
                if (Services is { } services)
                {
                    // The hooks service outlives every session: it is released after the engines stopped.
                    await services.DisposeAsync().ConfigureAwait(true);
                }
            }
        }
    }

    private void DisposeModels()
    {
        try
        {
            Board.Dispose();
        }
        finally
        {
            try
            {
                Panel.Dispose();
            }
            finally
            {
                try
                {
                    Catalog.Dispose();
                }
                finally
                {
                    try
                    {
                        _lifetime.Dispose();
                    }
                    finally
                    {
                        // Released last: the head's reference keeps the call gate, pane and handler open until the sessions and Cowork have stopped. Every provider call waits on the gate, so an earlier release would close it under calls still running.
                        BrowserUse?.Release();
                    }
                }
            }
        }
    }

    [SuppressMessage("Maintainability", "MA0051", Justification = "The acquire, transfer and cleanup steps stay in one flow so the cleanup guarantee sits beside the owners it protects.")]
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: each owner is nulled once transferred to its composed holder; the finally disposes any owner still held directly.")]
    private static async Task<AgentHead> BuildAsync(SessionEngineSetOptions options, Action<Action>? dispatch, bool selectMostRecent, TimeSpan detailsInterval, FileSettingsStore store, HeadSettingsOptions settingsOptions, Autonogrammer.Native.Composition.HeadServices? services, BrowserUseCompositionResult browser)
    {
        IDisposable? ownedModels = null;
        SessionEngineSet? host = null;
        HeadSettings? settings = null;
        CoworkHead? cowork = null;
        var completed = false;
        try
        {
            ownedModels = settingsOptions.Owned;
            host = new SessionEngineSet(options);
            settings = HeadSettings.Create(host, store, options.TimeProvider, dispatch, settingsOptions);
            ownedModels = null;
            cowork = await CreateCoworkAsync(options, services, browser.Host).ConfigureAwait(true);
            var head = await BuildModelsAsync(host, settings, cowork, options, dispatch, selectMostRecent, detailsInterval, services, browser).ConfigureAwait(true);
            host = null;
            settings = null;
            cowork = null;
            completed = true;
            return head;
        }
        finally
        {
            ExceptionDispatchInfo? failure = null;
            try
            {
                if (cowork is not null)
                {
                    await cowork.DisposeAsync().ConfigureAwait(true);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (settings is not null)
                {
                    await settings.DisposeAsync().ConfigureAwait(true);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (host is not null)
                {
                    await host.DisposeAsync().ConfigureAwait(true);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (ownedModels is not null)
                {
                    ownedModels.Dispose();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            CleanupFailureReporter.Report(failure, completed, options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Agents"));
        }
    }

    private static Task<CoworkHead> CreateCoworkAsync(SessionEngineSetOptions options, Autonogrammer.Native.Composition.HeadServices? services, ICoworkToolHost? coworkTools) =>
        CoworkHead.CreateAsync(
            options.Loop.StateRoot,
            options.Loop.WorkspaceRoot,
            new SignedOutCoworkGate(),
            options.TransportFactory,
            options.LoggerFactory,
            options.OmpPath,
            options.TimeProvider,
            options.ProviderKeys,
            services?.Mcp,
            coworkTools,
            services is null ? null : ct => services.ResolveOmpForLaunchAsync(ct));

    [SuppressMessage("Maintainability", "MA0051", Justification = "The acquire, transfer and cleanup steps stay in one flow so the cleanup guarantee sits beside the owners it protects.")]
    private static async Task<AgentHead> BuildModelsAsync(SessionEngineSet host, HeadSettings settings, CoworkHead cowork, SessionEngineSetOptions options, Action<Action>? dispatch, bool selectMostRecent, TimeSpan detailsInterval, Autonogrammer.Native.Composition.HeadServices? services, BrowserUseCompositionResult browser)
    {
        SessionCatalogModel? catalog = null;
        AgentPanelViewModel? panel = null;
        KanbanBoardModel? board = null;
        IDisposable? loading = null;
        BrowserUseCompositionResult? retainedBrowser = null;
        var completed = false;
        try
        {
            catalog = new SessionCatalogModel(
                host.Catalog,
                options.TimeProvider,
                new SessionCatalogOptions { IsClosed = host.IsClosed, Dispatch = dispatch });
            panel = new AgentPanelViewModel(catalog, host);
            board = new KanbanBoardModel(catalog, options.TimeProvider, KanbanOptions(host, options));
            host.CatalogModel = catalog;
            // A local automation starts its run in a session of this head, through the panel's own models.
            // Supplied services stay borrowed through registration; only the returned head owns them.
            services?.Automations.Attach(new HeadAutomationSessions(host, dispatch));
            loading = panel.BeginInitialising();
            retainedBrowser = browser.Retain();
            var head = CompleteHead(host, catalog, panel, board, settings, cowork, loading, retainedBrowser, options, dispatch, selectMostRecent, detailsInterval, services);
            // The head owns the models from here on.
            retainedBrowser = null;
            loading = null;
            board = null;
            panel = null;
            catalog = null;
            completed = true;
            return head;
        }
        finally
        {
            ExceptionDispatchInfo? failure = null;
            try
            {
                if (retainedBrowser is not null)
                {
                    retainedBrowser.Release();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (loading is not null)
                {
                    loading.Dispose();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (board is not null)
                {
                    board.Dispose();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (panel is not null)
                {
                    panel.Dispose();
                    await panel.SelectionLoad.WaitAsync(CancellationToken.None).ConfigureAwait(true);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            try
            {
                if (catalog is not null)
                {
                    catalog.Dispose();
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure ??= ExceptionDispatchInfo.Capture(exception);
            }

            CleanupFailureReporter.Report(failure, completed, options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Agents"));
        }
    }

    private static KanbanBoardOptions KanbanOptions(SessionEngineSet host, SessionEngineSetOptions options) =>
        new()
        {
            Details = new HeadKanbanDetailsSource(host),
            Steering = new HeadCardSteering(host, options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Agents")),
            StartLoading = true,
        };

    private static AgentHead CompleteHead(SessionEngineSet host, SessionCatalogModel catalog, AgentPanelViewModel panel, KanbanBoardModel board, HeadSettings settings, CoworkHead cowork, IDisposable loading, BrowserUseCompositionResult browser, SessionEngineSetOptions options, Action<Action>? dispatch, bool selectMostRecent, TimeSpan detailsInterval, Autonogrammer.Native.Composition.HeadServices? services)
    {
        var bundles = ConfigureHost(host, options, services);
        return new AgentHead(host, catalog, panel, board, loading, selectMostRecent, dispatch, options.TimeProvider, detailsInterval, settings)
        {
            Cowork = cowork,
            Bundles = bundles,
            BrowserUse = browser,
            Services = services,
        };
    }

    private static Autonogrammer.Native.Composition.Plugins.BundleComposition ConfigureHost(SessionEngineSet host, SessionEngineSetOptions options, Autonogrammer.Native.Composition.HeadServices? services)
    {
        var bundles = new Autonogrammer.Native.Composition.Plugins.BundleComposition(
            Path.Combine(options.Loop.StateRoot, "plugins"),
            options.Loop.StateRoot);
        Autonogrammer.Native.Composition.Plugins.BundleComposition.PluginRootsFor = remote => bundles.PluginRoots(remote);
        host.RegisterLaunchContributor(bundles.Contributor);
        host.RegisterLaunchContributor(new Autonogrammer.Harness.Isolation.IsolationContributor(
            new Autonogrammer.Harness.Isolation.IsolationDecision(Autonogrammer.Harness.Isolation.IsolationLevel.None, null, null, false)));
        host.Activity = new Autonogrammer.Native.Composition.Activity.ActivityComposition(options.Loop.StateRoot);
        if (services is not null)
        {
            services.UsageHistory = host.Activity.UsageHistory;
        }

        return bundles;
    }

    private static void UiDispatch(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    /// <summary>Starts the timer that reads the Substrate fields of the cards, and reads them once now. An interval that is not positive, or infinite, does neither.</summary>
    private void StartDetailsRefresh()
    {
        if (_detailsInterval <= TimeSpan.Zero || _detailsInterval == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) != 0 || _detailsTimer is not null)
            {
                return;
            }

            _detailsTimer = _clock.CreateTimer(static state => ((AgentHead)state!).OnDetailsTick(), this, _detailsInterval, _detailsInterval);
        }

        OnDetailsTick();
    }

    /// <summary>Reads the Substrate fields of the cards on the context that owns the models, unless the previous read is still running.</summary>
    private void OnDetailsTick()
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
        {
            return;
        }

        void Start() => Volatile.Write(ref _refreshTask, RefreshDetailsAsync());

        if (_dispatch is null)
        {
            Start();
        }
        else
        {
            _dispatch(Start);
        }
    }

    private async Task RefreshDetailsAsync()
    {
        try
        {
            await Board.RefreshDetailsAsync(_lifetime.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The head is closing.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A failed read leaves the fields as they were: the cards show what Substrate last reported, and the next tick reads again.
        }
        finally
        {
            Volatile.Write(ref _refreshing, 0);
        }
    }

    private sealed class SignedOutCoworkGate : Autonogrammer.Cowork.ICoworkAccountGate
    {
        public bool IsSignedIn => false;

        public string? AccountFolderKey => null;
    }
}

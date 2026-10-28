using Avalonia.Threading;
using Autonogrammer.Native.Composition.Automations;
using Autonogrammer.Native.Logging;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Settings.Models;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Presentation.Settings.Models;

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

    /// <summary>Builds the head of the running process: the plugins, the state and the Substrate come from the environment, and the registry's changes reach the models on the UI thread.</summary>
    /// <returns>The head. It opens nothing until <see cref="InitializeAsync"/>.</returns>
    public static AgentHead CreateProduction() => CreateProduction(null);

    private static AgentHead CreateProduction(Autonogrammer.Native.Composition.HeadServices? services)
    {
        var loop = AgentLoopEnvironment.Resolve();
        var store = new FileSettingsStore(loop.StateRoot);

        // The Models page and the provider keys omp is started with are built together, so a key saved on the page reaches every omp the head starts.
        var models = ModelsSettingsComposition.Create(new ModelsSettingsCompositionOptions { StateRoot = loop.StateRoot });
        try
        {
            return Build(
                new SessionEngineSetOptions
                {
                    Loop = loop,
                    AllowedKinds = Program.ProductionAllowedKinds,
                    LoggerFactory = DesktopLogging.Factory,
                    Motion = HeadSettings.ReadOrDefaults(store).Motion,
                    ProviderKeys = models.Keys,
                    Services = services,
                },
                UiDispatch,
                selectMostRecent: true,
                DefaultDetailsRefreshInterval,
                store,
                new HeadSettingsOptions
                {
                    ModelsPageFactory = () => new ModelsSettingsPage(models.Source),
                    NetworkSource = NetworkDiagnosticsSource.Over(models.Omp.ModelsFile),
                    Owned = models,
                    SweepInterval = SessionLifecycleSweeper.DefaultInterval,
                    Services = services,
                },
                services);
        }
        catch
        {
            models.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Builds the head of the running process like <see cref="CreateProduction()"/>, and first opens the services every window shares (hooks, remote environments, MCP catalog, checkpoints, run mode,
    /// cloud agents). The services are opened asynchronously, so the application shows its window once this has completed.
    /// </summary>
    /// <param name="topLevel">Returns the window the system browser is asked through, or null.</param>
    /// <param name="cancellationToken">Cancels the opening of the services.</param>
    /// <returns>The head. It opens nothing more until <see cref="InitializeAsync"/>.</returns>
    public static async Task<AgentHead> CreateProductionAsync(Func<Avalonia.Controls.TopLevel?> topLevel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        var loop = AgentLoopEnvironment.Resolve();
        var services = await Autonogrammer.Native.Composition.HeadServices.CreateAsync(
            new Autonogrammer.Native.Composition.HeadServicesOptions
            {
                StateRoot = loop.StateRoot,
                PluginsRoot = loop.PluginsRoot,
                WorkspaceRoot = loop.WorkspaceRoot,
                TopLevel = topLevel,
                LoggerFactory = DesktopLogging.Factory,
            },
            cancellationToken).ConfigureAwait(true);
        try
        {
            return CreateProduction(services);
        }
        catch
        {
            await services.DisposeAsync().ConfigureAwait(true);
            throw;
        }
    }

    /// <summary>Builds a head over explicit roots. Tests use it with a scripted transport.</summary>
    /// <param name="options">The roots and seams.</param>
    /// <returns>The head. It opens nothing until <see cref="InitializeAsync"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public static AgentHead Create(AgentHeadOptions options)
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
        return Build(
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
            },
            options.Services);
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
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

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
        await Settings.DisposeAsync().ConfigureAwait(true);

        // A run in flight works in a session of the host, so the automations stop before the engines.
        if (Services is { } automationServices)
        {
            await automationServices.Automations.DisposeAsync().ConfigureAwait(true);
        }

        await Host.DisposeAsync().ConfigureAwait(true);
        if (Services is { } services)
        {
            // The hooks service outlives every session: it is released after the engines stopped.
            await services.DisposeAsync().ConfigureAwait(true);
        }

        Board.Dispose();
        Panel.Dispose();
        Catalog.Dispose();
        _lifetime.Dispose();
    }

    private static AgentHead Build(SessionEngineSetOptions options, Action<Action>? dispatch, bool selectMostRecent, TimeSpan detailsInterval, FileSettingsStore store, HeadSettingsOptions settingsOptions, Autonogrammer.Native.Composition.HeadServices? services)
    {
        var host = new SessionEngineSet(options);
        SessionCatalogModel? catalog = null;
        AgentPanelViewModel? panel = null;
        KanbanBoardModel? board = null;
        try
        {
            catalog = new SessionCatalogModel(
                host.Catalog,
                options.TimeProvider,
                new SessionCatalogOptions { IsClosed = host.IsClosed, Dispatch = dispatch });
            panel = new AgentPanelViewModel(catalog, host);
            board = new KanbanBoardModel(
                catalog,
                options.TimeProvider,
                new KanbanBoardOptions
                {
                    Details = new HeadKanbanDetailsSource(host),
                    Steering = new HeadCardSteering(host, options.LoggerFactory?.CreateLogger("Autonogrammer.Native.Agents")),
                    StartLoading = true,
                });
            var settings = HeadSettings.Create(host, store, options.TimeProvider, dispatch, settingsOptions);
            host.CatalogModel = catalog;

            // A local automation starts its run in a session of this head, through the panel's own models.
            services?.Automations.Attach(new HeadAutomationSessions(host, dispatch));
            var head = new AgentHead(host, catalog, panel, board, panel.BeginInitialising(), selectMostRecent, dispatch, options.TimeProvider, detailsInterval, settings) { Services = services };

            // The head owns the models from here on.
            catalog = null;
            panel = null;
            board = null;
            return head;
        }
        finally
        {
            board?.Dispose();
            panel?.Dispose();
            catalog?.Dispose();
        }
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
}

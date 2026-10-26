using Avalonia.Threading;
using OmpOS.Native.Logging;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;

namespace OmpOS.Native.Agents;

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
        TimeSpan detailsInterval)
    {
        Host = host;
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

    /// <summary>Builds the head of the running process: the plugins, the state and the Substrate come from the environment, and the registry's changes reach the models on the UI thread.</summary>
    /// <returns>The head. It opens nothing until <see cref="InitializeAsync"/>.</returns>
    public static AgentHead CreateProduction() => Build(
        new SessionEngineSetOptions
        {
            Loop = AgentLoopEnvironment.Resolve(),
            AllowedKinds = Program.ProductionAllowedKinds,
            LoggerFactory = DesktopLogging.Factory,
            Motion = MotionPreference.System,
        },
        UiDispatch,
        selectMostRecent: true,
        DefaultDetailsRefreshInterval);

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
            },
            options.Dispatch,
            options.SelectMostRecentSession,
            options.DetailsRefreshInterval ?? DefaultDetailsRefreshInterval);
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
        await Host.DisposeAsync().ConfigureAwait(true);
        Board.Dispose();
        Panel.Dispose();
        Catalog.Dispose();
        _lifetime.Dispose();
    }

    private static AgentHead Build(SessionEngineSetOptions options, Action<Action>? dispatch, bool selectMostRecent, TimeSpan detailsInterval)
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
                    Steering = new HeadCardSteering(host, options.LoggerFactory?.CreateLogger("OmpOS.Native.Agents")),
                    StartLoading = true,
                });
            var head = new AgentHead(host, catalog, panel, board, panel.BeginInitialising(), selectMostRecent, dispatch, options.TimeProvider, detailsInterval);

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

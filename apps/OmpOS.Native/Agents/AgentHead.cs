using Avalonia.Threading;
using OmpOS.Native.Logging;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native.Agents;

/// <summary>
/// The composition of the Agent Panel for the native head: the engines of N sessions (<see cref="SessionEngineSet"/>), the rail model over
/// their registry and the panel view model the main view shows. The panel starts <see cref="PanelState.Loading"/> and stays so until
/// <see cref="InitializeAsync"/> has loaded the registry; a missing plugins root or an uninitialised submodule never stops it, because each
/// plugin then reports itself unavailable and the sessions run as plain omp.
/// </summary>
internal sealed class AgentHead : IAsyncDisposable
{
    private readonly IDisposable _loading;
    private readonly bool _selectMostRecent;
    private int _initialised;
    private int _disposed;

    private AgentHead(SessionEngineSet host, SessionCatalogModel catalog, AgentPanelViewModel panel, IDisposable loading, bool selectMostRecent)
    {
        Host = host;
        Catalog = catalog;
        _selectMostRecent = selectMostRecent;
        Panel = panel;
        _loading = loading;
    }

    /// <summary>Gets the engines and the registry.</summary>
    public SessionEngineSet Host { get; }

    /// <summary>Gets the rail model over the registry.</summary>
    public SessionCatalogModel Catalog { get; }

    /// <summary>Gets the panel view model the main view shows.</summary>
    public AgentPanelViewModel Panel { get; }

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
        UiDispatch);

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
            },
            options.Dispatch,
            options.SelectMostRecentSession);
    }

    /// <summary>
    /// Loads the plugins and the saved sessions, then selects the most recent session. A failure shows as the panel's Error state with
    /// its retry; it does not throw. Calling it again does nothing.
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
        }
    }

    /// <summary>Stops every engine, then releases the panel and the rail model. The registry keeps what it holds, so a running turn is found lost at the next start.</summary>
    /// <returns>A task that completes when every engine is stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _loading.Dispose();
        await Host.DisposeAsync().ConfigureAwait(true);
        Panel.Dispose();
        Catalog.Dispose();
    }

    private static AgentHead Build(SessionEngineSetOptions options, Action<Action>? dispatch, bool selectMostRecent = true)
    {
        var host = new SessionEngineSet(options);
        SessionCatalogModel? catalog = null;
        AgentPanelViewModel? panel = null;
        try
        {
            catalog = new SessionCatalogModel(
                host.Catalog,
                options.TimeProvider,
                new SessionCatalogOptions { IsClosed = host.IsClosed, Dispatch = dispatch });
            panel = new AgentPanelViewModel(catalog, host);
            var head = new AgentHead(host, catalog, panel, panel.BeginInitialising(), selectMostRecent);

            // The head owns the models from here on.
            catalog = null;
            panel = null;
            return head;
        }
        finally
        {
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
}

using Avalonia.Controls;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Tests.Terminal;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Native.Views.Shell;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Native.Tests.AgentPanel.Shell;

/// <summary>
/// A whole Agent Panel under test: a session registry, a catalog model over it, a panel view model, a scripted head that creates one
/// runtime per session, and the main window over them. Tests add sessions, show the window and read the runtimes.
/// </summary>
internal sealed class ShellFixture : IAsyncDisposable
{
    private int _added;
    private bool _disposed;

    private ShellFixture()
    {
        Clock = new ShellClock();
        Sessions = new FakeAgentCatalog();
        Host = new ScriptedShellHost(Sessions, Clock);
        Catalog = new SessionCatalogModel(
            Sessions,
            Clock,
            new SessionCatalogOptions
            {
                IsClosed = id => Host.ClosedIds.Contains(id),
                LiveStatus = LiveStatusOf,
            });
        Panel = new AgentPanelViewModel(Catalog, Host);
    }

    /// <summary>Gets the clock of the shell.</summary>
    public ShellClock Clock { get; }

    /// <summary>Gets the session registry the catalog reads.</summary>
    public FakeAgentCatalog Sessions { get; }

    /// <summary>Gets the scripted head.</summary>
    public ScriptedShellHost Host { get; }

    /// <summary>Gets the rail model.</summary>
    public SessionCatalogModel Catalog { get; }

    /// <summary>Gets the panel view model the main view shows.</summary>
    public AgentPanelViewModel Panel { get; }

    /// <summary>Gets the main window, or null before <see cref="Show"/>.</summary>
    public MainWindow? Window { get; private set; }

    /// <summary>Gets the main view of the window.</summary>
    public MainView Main => (MainView)Window!.Content!;

    /// <summary>Gets the Kanban board of <see cref="ShowWithBoard"/>, or null before it.</summary>
    public KanbanBoardModel? Board { get; private set; }

    /// <summary>Gets the plain window of <see cref="ShowWithBoard"/>, or null before it.</summary>
    public Window? BoardWindow { get; private set; }

    /// <summary>Gets the main view of <see cref="ShowWithBoard"/>, or null before it.</summary>
    public MainView? BoardMain { get; private set; }

    /// <summary>Gets the plain window of <see cref="ShowWithServices"/>, or null before it.</summary>
    public Window? ServicesWindow { get; private set; }

    /// <summary>Gets the main view of <see cref="ShowWithServices"/>, or null before it.</summary>
    public MainView? ServicesMain { get; private set; }

    /// <summary>Creates a fixture with no session.</summary>
    /// <returns>The fixture.</returns>
    public static ShellFixture Create() => new();

    /// <summary>Creates a fixture with one selected session over the script. Without a script the session has an unconnected send path.</summary>
    /// <param name="script">The session's script, or null for an unconnected send path.</param>
    /// <returns>The fixture. Its first session is <c>session-1</c>.</returns>
    public static ShellFixture CreateWithSession(SessionScript? script = null)
    {
        var fixture = new ShellFixture();
        fixture.AddSession("session-1", "Agent one", script: script);
        fixture.Panel.SelectSession("session-1");
        return fixture;
    }

    /// <summary>Adds a session to the registry and refreshes the rail. The panel opens its runtime when the session is first selected.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="title">The session title.</param>
    /// <param name="lifecycle">The persisted lifecycle.</param>
    /// <param name="script">The session's script, or null for an unconnected send path.</param>
    /// <param name="taskId">The task the session works on, or null.</param>
    /// <param name="graphId">The Graph identifier of the session, or null.</param>
    /// <returns>The session summary that was added.</returns>
    public SessionSummary AddSession(
        string id,
        string title,
        SessionLifecycle lifecycle = SessionLifecycle.Created,
        SessionScript? script = null,
        string? taskId = null,
        string? graphId = null)
    {
        if (script is not null)
        {
            Host.Script(id, script);
        }

        _added++;
        var summary = new SessionSummary
        {
            Id = id,
            Title = title,
            Lifecycle = lifecycle,
            CreatedAt = Clock.Now - TimeSpan.FromMinutes(10 - _added),
            LastActivity = Clock.Now - TimeSpan.FromMinutes(10 - _added),
            TaskId = taskId,
            GraphId = graphId,
        };
        Sessions.Put(summary);
        Catalog.Refresh();
        return summary;
    }

    /// <summary>Adds an omp terminal session to the registry over a fake omp starter and a fake steering channel, and refreshes the rail.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="title">The session title.</param>
    /// <param name="script">The session's script. Null gives the session a fake omp starter and a fake steering channel.</param>
    /// <param name="taskId">The task the session works on, or null.</param>
    /// <param name="lifecycle">The persisted lifecycle.</param>
    /// <returns>The session summary that was added.</returns>
    public SessionSummary AddTerminalSession(string id, string title, SessionScript? script = null, string? taskId = null, SessionLifecycle lifecycle = SessionLifecycle.Created)
    {
        Host.Script(id, script ?? new SessionScript { TerminalStarter = new FakeOmpTerminalStarter(), Steering = new FakeSteeringChannel() });
        _added++;
        var summary = new SessionSummary
        {
            Id = id,
            Title = title,
            Lifecycle = lifecycle,
            CreatedAt = Clock.Now - TimeSpan.FromMinutes(10 - _added),
            LastActivity = Clock.Now - TimeSpan.FromMinutes(10 - _added),
            Kind = SessionKind.Terminal,
            TaskId = taskId,
        };
        Sessions.Put(summary);
        Catalog.Refresh();
        return summary;
    }

    /// <summary>Gets the runtime of a session. The panel must have opened it: select the session first.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The runtime.</returns>
    public AgentSessionRuntime Runtime(string sessionId) => Host.Runtimes[sessionId];

    /// <summary>Gets the script of a session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The script.</returns>
    public SessionScript Script(string sessionId) => Host.ScriptOf(sessionId);

    /// <summary>Shows the main window over the panel and gives it the layout passes a real window gets.</summary>
    /// <param name="motion">The motion setting of the window.</param>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <param name="workspaceRoot">The workspace folder of the window's tools, or null for the current folder.</param>
    /// <returns>The shown window.</returns>
    public MainWindow Show(MotionPreference motion = MotionPreference.System, double width = 1280, double height = 800, string? workspaceRoot = null)
    {
        var window = new MainWindow(Panel, motion, workspaceRoot)
        {
            Width = width,
            Height = height,
        };
        window.Show();
        ShellHarness.Settle(window);
        ShellHarness.Settle(window);
        Window = window;
        return window;
    }

    /// <summary>
    /// Shows the main view with a Kanban board over the same catalog, in a plain window, the way the head builds it: the tab strip exists
    /// and the board is the head's. The head's own window shell is not involved.
    /// </summary>
    /// <param name="motion">The motion setting of the view.</param>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <returns>The main view. The fixture owns the window, the view and the board.</returns>
    public MainView ShowWithBoard(MotionPreference motion = MotionPreference.System, double width = 1280, double height = 800)
    {
        Board = new KanbanBoardModel(Catalog, Clock);
        var main = new MainView(Panel, null, motion, Board);
        var window = new Window { Content = main, Width = width, Height = height };
        window.Show();
        ShellHarness.Settle(window);
        ShellHarness.Settle(window);
        BoardWindow = window;
        BoardMain = main;
        return main;
    }

    /// <summary>Shows the main view with the services a head supplies to the shell (hooks, remote environments, checkpoints, cloud and the others), in a plain window.</summary>
    /// <param name="services">The services.</param>
    /// <param name="motion">The motion setting of the view.</param>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <returns>The main view. The fixture owns the window and the view.</returns>
    public MainView ShowWithServices(ShellServices services, MotionPreference motion = MotionPreference.Full, double width = 1280, double height = 800)
    {
        ArgumentNullException.ThrowIfNull(services);

        var main = new MainView(Panel, null, motion, mcpHost: null, board: null, services);
        var window = new Window { Content = main, Width = width, Height = height };
        window.Show();
        ShellHarness.Settle(window);
        ShellHarness.Settle(window);
        ServicesWindow = window;
        ServicesMain = main;
        return main;
    }

    /// <summary>Resizes the shown window and settles its layout.</summary>
    /// <param name="width">The new width.</param>
    /// <param name="height">The new height.</param>
    public void Resize(double width, double height)
    {
        var window = Window!;
        window.Width = width;
        window.Height = height;
        ShellHarness.Settle(window);
        ShellHarness.Settle(window);
    }

    /// <summary>Stops the sessions the panel opened, disposes the panel and closes the window.</summary>
    /// <returns>A task that completes when everything stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Window?.Close();
        BoardMain?.Close();
        ServicesMain?.Close();
        ServicesWindow?.Close();
        BoardWindow?.Close();
        Board?.Dispose();
        Panel.Dispose();
        Catalog.Dispose();
        foreach (var runtime in Host.Runtimes.Values)
        {
            await runtime.DisposeAsync().ConfigureAwait(true);
        }
    }

    private RuntimeStatus? LiveStatusOf(string id)
    {
        if (!Host.Runtimes.TryGetValue(id, out var runtime))
        {
            return null;
        }

        return runtime.ViewModel.Steering.IsRunActive ? RuntimeStatus.Running : null;
    }
}

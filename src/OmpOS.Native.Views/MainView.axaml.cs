using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Harness.Mcp;
using OmpOS.Harness.Terminal;
using OmpOS.Native.Engine;
using OmpOS.Native.Views;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Native.Views.Chats;
using OmpOS.Native.Views.Diff;
using OmpOS.Native.Views.Editor;
using OmpOS.Native.Views.Explorer;
using OmpOS.Native.Views.Export;
using OmpOS.Native.Views.InlineEditing;
using OmpOS.Native.Views.Kanban;
using OmpOS.Native.Views.Mcp;
using OmpOS.Native.Views.RunControls;
using OmpOS.Native.Views.Search;
using OmpOS.Native.Views.Sessions;
using OmpOS.Native.Views.Terminal;
using OmpOS.Native.Views.Workspace;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Agents;
using OmpOS.Presentation.Chats;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Export;
using OmpOS.Presentation.Kanban;
using OmpOS.Presentation.State;
using OmpOS.Workspace;

namespace OmpOS.Native;

/// <summary>
/// The main content of the native head: the Agent Panel. It hosts two regions: the Agents rail (the session list, the New agent and New
/// omp terminal controls and the tool toggles) and the thread of the selected agent (header, the one-line plugin connection row,
/// transcript or the omp terminal of a terminal session, transcript search, tool approvals, composer or the steer-only terminal
/// composer, side chats, or the Agents view or a workspace tool that opens in place of the transcript). There is no region beside them.
/// When the head supplies a Kanban board a tab strip above them switches the whole area between the Agents layout and the Kanban tab.
/// It binds the presentation view models the web head binds, and holds no engine:
/// the head creates one <see cref="AgentSessionRuntime"/> per session and the panel view model it hands in selects among them.
/// </summary>
/// <remarks>
/// <para>
/// Selecting a session rebinds the thread region to that session; the sessions that are not shown keep running. The workspace is the
/// folder the view was given. Its tool models are created when a tool first opens and kept until the view closes, so a view that
/// opens no tool reads no folder and starts no process.
/// </para>
/// <para>The view has no window of its own: the host window shows it and calls <see cref="Close"/> when it closes. The head owns the panel and the runtimes and disposes them.</para>
/// </remarks>
public partial class MainView : UserControl
{
    /// <summary>The status line text after a send the host refuses (UI-SPEC, Copywriting Contract).</summary>
    public const string RefusedSendStatus = "Message not sent. Sending is unavailable.";

    /// <summary>The status line text after Stop with no response running (UI-SPEC, Copywriting Contract).</summary>
    public const string NoResponseStatus = "No response is running.";

    /// <summary>The status line text prefix after a composer action faults. The fault's own message follows it.</summary>
    public const string FaultedStatus = "Something went wrong.";

    /// <summary>The status line text prefix after a workspace tool cannot open. The tool's name and the reason follow it.</summary>
    public const string ToolFailedStatus = "Could not open";

    /// <summary>The banner text prefix after the engine stream fails. The failure's own message follows it.</summary>
    public const string EngineFailedBanner = "This turn failed:";

    /// <summary>The style class the view carries under <see cref="MotionPreference.Reduced"/>. A head's styles select it to remove transitions.</summary>
    public const string ReducedMotionClass = "reduced-motion";

    /// <summary>The window width from which the rail is wide (UI-SPEC, Responsive rules).</summary>
    public const double WideLayoutWidth = 1200;

    /// <summary>The window height under which the composer hint line is hidden (UI-SPEC, Responsive rules).</summary>
    public const double ShortWindowHeight = 720;

    /// <summary>The tooltip of the Settings button in the rail footer.</summary>
    public const string SettingsTooltip = "Settings (Ctrl+,)";

    /// <summary>The level-one heading of a transcript exported from the shell.</summary>
    private const string ExportTitle = "OmpOS session";

    /// <summary>The style class of the rail while the layout is wide.</summary>
    private const string WideClass = "wide";

    /// <summary>The region index of the Agents rail, for focus movement.</summary>
    private const int RailRegionIndex = 0;

    /// <summary>The region index of the thread, for focus movement.</summary>
    private const int ThreadRegionIndex = 1;

    private readonly AgentPanelViewModel _panel;
    private readonly WorkspaceRoot _workspace;
    private readonly EditorTabs _editorTabs = new();
    private readonly ToolEntry _editorTool;
    private readonly List<ToolEntry> _tools;
    private readonly Dictionary<string, SessionPane> _panes = new(StringComparer.Ordinal);
    private readonly AgentRailView _rail;
    private readonly PanelStateView _stateView;
    private readonly KanbanBoardModel? _board;
    private readonly KanbanBoardView? _boardView;
    private bool _kanbanShown;
    private bool _syncingTabs;
    private ToolEntry? _openTool;
    private ToolEntry? _terminalTool;
    private bool _terminalSettingsStale;
    private Transitions? _topTabsTransitions;
    private Transitions? _agentsTabTransitions;
    private Transitions? _kanbanTabTransitions;
    private bool _tabTransitionsRemoved;
    private ToggleButton? _openedBy;
    private bool _syncingTools;
    private bool _syncingRail;
    private bool _sized;
    private bool _wide;
    private bool _hintVisible = true;
    private SessionPane? _current;
    private AgentSessionViewModel? _watched;
    private WorkspaceTree? _explorer;
    private WorkspaceCatalog? _catalog;
    private WorkspaceConventionsModel? _conventions;
    private WorkspaceSearchModel? _workspaceSearch;
    private IMcpRegistryHost? _mcpHost;
    private McpToolRegistry? _mcpRegistry;
    private McpToolsModel? _mcpTools;
    private bool _closed;
    private TopLevel? _keyHost;

    /// <summary>
    /// Creates the main view over an <see cref="UnconnectedAgentPanel"/> in the current folder: no engine is connected, and every send and
    /// every approval is refused. Avalonia's XAML loader and the designer construct the view through this constructor.
    /// </summary>
    public MainView()
        : this(UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot: null, MotionPreference.System))
    {
    }

    /// <summary>Creates the main view over the Agent Panel the head built.</summary>
    /// <param name="panel">The panel view model: the session rail and the selected session. The head owns it and disposes it.</param>
    /// <param name="workspaceRoot">The workspace folder the view's tools work in, or null for the current folder. It must exist.</param>
    /// <param name="motion">The motion setting of the head. Only <see cref="MotionPreference.Reduced"/> puts the <see cref="ReducedMotionClass"/> class on the view, which the head's styles read to remove transitions.</param>
    /// <param name="board">The Kanban board the head built over the sessions, or null for a view with no Kanban tab. The head owns it and disposes it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is empty or white space.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="workspaceRoot"/> does not exist.</exception>
    public MainView(AgentPanelViewModel panel, string? workspaceRoot = null, MotionPreference motion = MotionPreference.System, KanbanBoardModel? board = null)
        : this(panel, workspaceRoot, motion, mcpHost: null, board)
    {
    }

    /// <summary>Creates the view with an MCP host. The view disposes the host when it closes; tests inject a host whose disposal fails.</summary>
    /// <param name="panel">The panel view model.</param>
    /// <param name="workspaceRoot">The workspace folder, or null for the current folder.</param>
    /// <param name="motion">The motion setting of the head.</param>
    /// <param name="mcpHost">The MCP host the view lists and disposes, or null to create the production registry when the MCP view opens.</param>
    /// <param name="board">The Kanban board, or null for a view with no Kanban tab.</param>
    internal MainView(AgentPanelViewModel panel, string? workspaceRoot, MotionPreference motion, IMcpRegistryHost? mcpHost, KanbanBoardModel? board = null)
    {
        ArgumentNullException.ThrowIfNull(panel);

        _panel = panel;
        _mcpHost = mcpHost;
        _workspace = new WorkspaceRoot(workspaceRoot ?? Environment.CurrentDirectory);

        InitializeComponent();
        Classes.Set(ReducedMotionClass, motion == MotionPreference.Reduced);
        if (motion == MotionPreference.Reduced)
        {
            RemoveTabTransitions();
        }

        // The rail: the sessions of the catalog, the New agent control and the loading and error copy of the panel.
        _rail = (AgentRailView)ViewMapping.Create(panel.Catalog);
        _rail.Panel = panel;
        _rail.NewAgentCompleted += OnNewAgentCompleted;
        _rail.NewTerminalCompleted += OnNewAgentCompleted;
        _rail.RowActivated += OnRowActivated;
        _rail.CloseRequested += OnRailCloseRequested;
        RailHost.Content = _rail;

        // The panel state block (empty, loading, error) sits above the thread and hides itself while the panel is ready.
        _stateView = (PanelStateView)ViewMapping.Create(panel);
        _stateView.NewAgentCompleted += OnNewAgentCompleted;
        PanelStateHost.Content = _stateView;

        SessionsViewToggle.IsCheckedChanged += OnRailViewToggleChanged;
        ToolsViewToggle.IsCheckedChanged += OnRailViewToggleChanged;
        SizeChanged += OnLayoutSizeChanged;

        // The Agents window: one registry of runs and one view model over it. The view opens in place of the transcript.
        AgentRuns = new AgentRunRegistry();
        Agents = new AgentsViewModel(AgentRuns);
        AgentsHost.Content = ViewMapping.Create(Agents);
        AgentsToggle.IsCheckedChanged += OnAgentsToggleChanged;
        SideChatsToggle.IsCheckedChanged += OnSideChatsToggleChanged;
        SuggestionsToggle.IsCheckedChanged += OnSuggestionsToggleChanged;
        SettingsButton.Click += OnSettingsClick;

        // The editor model is shared by the editor tool and the explorer, which opens files into it.
        EditorModel = new EditorViewModel(_editorTabs);
        _editorTool = new ToolEntry(EditorToggle, "Editor", () => ViewMapping.Create(EditorModel), keepView: true);
        _tools = CreateTools();

        // The Kanban tab: the tab strip exists only when the head supplied a board.
        if (board is not null)
        {
            _board = board;
            _boardView = (KanbanBoardView)ViewMapping.Create(board);
            KanbanHost.Content = _boardView;
            ShowKanbanTabStrip(board);
        }

        _panel.PropertyChanged += OnPanelChanged;
        _panel.Catalog.PropertyChanged += OnCatalogChanged;
        BindSelected();
    }

    // The tab strip is the one control of the view the head's styles do not know: it takes no transition under reduced motion.
    private void RemoveTabTransitions()
    {
        if (_tabTransitionsRemoved)
        {
            return;
        }

        _topTabsTransitions = TopTabs.Transitions;
        _agentsTabTransitions = AgentsTab.Transitions;
        _kanbanTabTransitions = KanbanTab.Transitions;
        TopTabs.Transitions = new Transitions();
        AgentsTab.Transitions = new Transitions();
        KanbanTab.Transitions = new Transitions();
        _tabTransitionsRemoved = true;
    }

    private void RestoreTabTransitions()
    {
        if (!_tabTransitionsRemoved)
        {
            return;
        }

        TopTabs.Transitions = _topTabsTransitions;
        AgentsTab.Transitions = _agentsTabTransitions;
        KanbanTab.Transitions = _kanbanTabTransitions;
        _tabTransitionsRemoved = false;
    }

    // The tab strip shows only for a view that has a board; the board's requests open or start sessions in the Agents tab.
    private void ShowKanbanTabStrip(KanbanBoardModel board)
    {
        TopTabs.IsVisible = true;
        TopTabs.SelectionChanged += OnTopTabChanged;
        board.OpenSessionRequested += OnBoardOpenSessionRequested;
        board.StartSessionRequested += OnBoardStartSessionRequested;
    }

    /// <summary>Gets the panel view model the view shows.</summary>
    public AgentPanelViewModel Panel => _panel;

    /// <summary>Gets whether the Kanban tab is the selected top-level tab. It is false for a view with no board.</summary>
    public bool IsKanbanShown => _kanbanShown;

    /// <summary>Occurs when the user asks for the Settings window, with the rail footer button or Ctrl+,. The host that owns the window handles it; the view has no dependency on it.</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>
    /// Gets or sets where the Terminal tool reads its settings. It is read each time the tool builds its view, so the stored auto-run choice
    /// is the source of truth. Null builds the tool with the defaults, which never run a command without an approval.
    /// </summary>
    public Func<TerminalSettings>? TerminalSettingsProvider { get; set; }

    /// <summary>Asks for the Settings window, as the rail button and Ctrl+, do.</summary>
    public void RequestSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Applies a motion setting to the shell now: the reduced-motion class of the view and the tab strip transitions. Sessions that are
    /// already open keep the motion they were built with.
    /// </summary>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    public void ApplyMotion(MotionPreference motion)
    {
        var reduced = motion == MotionPreference.Reduced;
        Classes.Set(ReducedMotionClass, reduced);
        if (reduced)
        {
            RemoveTabTransitions();
        }
        else
        {
            RestoreTabTransitions();
        }
    }

    /// <summary>
    /// Tells the Terminal tool that its settings changed. A terminal panel with no proposal waiting and no command running is built again
    /// from the current settings at once; one that is busy keeps its settings until it is idle and the tool is opened again.
    /// </summary>
    public void ApplyTerminalSettings()
    {
        _terminalSettingsStale = true;
        RebuildTerminalIfIdle();
    }

    /// <summary>Listens for the keyboard map on the top level, so a shortcut works while nothing in the view has focus.</summary>
    /// <param name="e">The attachment data.</param>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _keyHost = TopLevel.GetTopLevel(this);
        _keyHost?.AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    /// <summary>Stops listening for the keyboard map.</summary>
    /// <param name="e">The detachment data.</param>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _keyHost?.RemoveHandler(KeyDownEvent, OnKeyDownTunnel);
        _keyHost = null;

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Gets the registry of the agent runs the Agents window shows.</summary>
    public AgentRunRegistry AgentRuns { get; }

    /// <summary>Gets the Agents window state: the runs that match the status filter and the open run view.</summary>
    public AgentsViewModel Agents { get; }

    /// <summary>Gets the editor state the editor tool binds. The explorer opens its files here. It is disposed when the window closes.</summary>
    internal EditorViewModel EditorModel { get; }

    /// <summary>Gets the transcript search model of the selected session, or null when no session is selected.</summary>
    internal TranscriptSearchModel? Search => _current?.Search;

    /// <summary>Gets the side chats of the selected session, or null when no session is selected.</summary>
    internal SideChatPanel? SideChats => _current?.SideChats;

    private void SetStatus(string text)
    {
        if (_current?.Runtime is { } runtime)
        {
            runtime.ReportStatus(text);
            return;
        }

        ShowStatus(text);
    }

    private void ShowStatus(string text)
    {
        StatusLine.Text = text;
        StatusLine.IsVisible = text.Length > 0;
    }

    /// <summary>Shows the feedback of the selected session's runtime: its status line text and its engine error banner.</summary>
    private void RefreshFeedback()
    {
        var runtime = _current?.Runtime;
        ShowStatus(runtime?.StatusText ?? string.Empty);
        var error = runtime?.ErrorText;
        ErrorBanner.Text = error ?? string.Empty;
        ErrorBanner.IsVisible = error is not null;
    }

    private void OnPanelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(AgentPanelViewModel.Selected):
                BindSelected();
                break;
            case nameof(AgentPanelViewModel.PipelineAnnouncement):
                ForwardAnnouncement();
                break;
            case nameof(AgentPanelViewModel.ActionError):
                if (_panel.ActionError is { } failure)
                {
                    SetStatus(failure);
                }

                break;
            default:
                break;
        }
    }

    private void OnCatalogChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SessionCatalogModel.Sessions), StringComparison.Ordinal))
        {
            PruneStalePanes();
        }
    }

    /// <summary>
    /// Shows a panel-level announcement (for example that an agent was closed) in the pipeline announcement line of the session that
    /// is shown. An announcement the session's own timeline made is already there.
    /// </summary>
    private void ForwardAnnouncement()
    {
        var text = _panel.PipelineAnnouncement;
        if (text is null || _current is not { } pane)
        {
            return;
        }

        if (!string.Equals(pane.ViewModel.Timeline.StatusAnnouncement, text, StringComparison.Ordinal))
        {
            pane.ViewModel.Timeline.Announce(text);
        }
    }

    // Rebinds the thread region and the feedback lines to the selected session. The sessions that are not shown keep
    // their own views, so a draft, a scroll position and an open search come back when the session is selected again.
    private void BindSelected()
    {
        var viewModel = _panel.Selected;
        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnSelectedViewModelChanged;
        }

        _watched = viewModel;
        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnSelectedViewModelChanged;
        }

        PruneStalePanes();
        ShowThread();

        _current = viewModel is null ? null : GetPane(viewModel);
        var pane = _current;
        ThreadHeaderHost.Content = pane?.Header;
        TranscriptHost.Content = pane is null ? null : pane.TerminalView ?? (Control)pane.Transcript;
        PipelineHost.Content = pane?.Strip;
        SearchHost.Content = pane?.SearchView;
        SearchHost.IsVisible = pane is { SearchOpen: true };
        RunControlsHost.Content = pane?.RunControls;
        ConfirmBarHost.Content = pane?.ConfirmBar;
        ComposerHost.Content = pane is null ? null : pane.SteerView ?? (Control)pane.Composer;
        SideChatHost.Content = pane?.SideChatView;
        if (pane is not null)
        {
            pane.Composer.SuggestionsShown = SuggestionsToggle.IsChecked == true;
            pane.Composer.HintVisible = _hintVisible;
        }

        RefreshAvailability();
        RefreshFeedback();
        ForwardAnnouncement();
    }

    // When a closed or lost agent comes back (Reopen agent, Resume agent) the composer returns and focus moves to its prompt.
    private void OnSelectedViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(AgentSessionViewModel.IsClosed) or nameof(AgentSessionViewModel.IsLost)))
        {
            return;
        }

        var hadComposer = ComposerHost.IsVisible;
        RefreshAvailability();
        if (!hadComposer && ComposerHost.IsVisible)
        {
            FocusThread();
        }
    }

    // A closed or lost session has no engine to send to: the composer hides and the closed notice shows instead.
    private void RefreshAvailability()
    {
        var viewModel = _current?.ViewModel;
        var unavailable = viewModel is { IsClosed: true } or { IsLost: true };
        ComposerHost.IsVisible = viewModel is not null && !unavailable;
        ClosedNotice.IsVisible = viewModel is { IsClosed: true };
        ClosedHeading.Text = AgentPanelCopy.ClosedHeading;
        ClosedBody.Text = AgentPanelCopy.ClosedBody;
    }

    private SessionPane GetPane(AgentSessionViewModel viewModel)
    {
        if (_panes.TryGetValue(viewModel.Id, out var existing) && ReferenceEquals(existing.ViewModel, viewModel))
        {
            return existing;
        }

        if (existing is not null)
        {
            existing.Dispose();
        }

        var pane = CreatePane(viewModel);
        _panes[viewModel.Id] = pane;
        return pane;
    }

    private void PruneStalePanes()
    {
        var live = _panel.Catalog.Sessions.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in _panes.Keys.Where(known => !live.Contains(known) && !ReferenceEquals(_panes[known], _current)).ToList())
        {
            _panes[id].Dispose();
            _panes.Remove(id);
        }
    }

    private SessionPane CreatePane(AgentSessionViewModel viewModel)
    {
        var runtime = AgentSessionRuntime.For(viewModel);
        var pane = new SessionPane(viewModel, runtime)
        {
            Transcript = (SessionView)ViewMapping.Create(viewModel.Session),
            Composer = (ComposerView)ViewMapping.Create(viewModel.Composer),
            Header = (ThreadHeaderView)ViewMapping.Create(viewModel),
            ConfirmBar = new ConfirmBarView { DataContext = viewModel },
            Strip = new PipelineStripView { DataContext = viewModel.Timeline },
            Search = new TranscriptSearchModel(viewModel.Session),
            SideChats = new SideChatPanel(viewModel.Id),
        };
        pane.SearchView = (TranscriptSearchView)ViewMapping.Create(pane.Search);
        pane.SideChatView = ViewMapping.Create(pane.SideChats);
        if (runtime is not null)
        {
            AttachRuntime(pane, runtime);
        }

        AttachRunSummary(pane, viewModel);

        pane.Composer.Steering = viewModel.Steering;
        pane.Composer.WorkFaulted += (_, e) => ReportFor(pane, $"{FaultedStatus} {e.Exception.GetBaseException().Message}");
        pane.Composer.StatusMessage += (_, e) => ReportFor(pane, e.Message);
        pane.Composer.StopConfirmationRequested += (_, _) => pane.ConfirmBar.OpenStop();
        pane.Transcript.StopRequested += (_, _) => pane.Composer.StopResponse();
        pane.Transcript.RetryRequested += (_, _) => runtime?.RetryLastTurn();
        pane.Search.MatchSelected += (_, e) => pane.Transcript.ScrollToBlock(e.Match.BlockIndex);
        pane.Header.CloseClicked += (_, _) => pane.ConfirmBar.OpenClose();
        pane.Header.ShowPipelineRequested += (_, _) => pane.Strip.ShowDetails();
        pane.Header.TaskChipClicked += (_, _) => ShowKanbanTab(viewModel.Id);
        if (runtime?.Terminal is { } terminal)
        {
            // A terminal session shows omp in the thread and steers it from a steer-only composer. The views are kept with the pane, so
            // another tab or session never restarts omp: the runtime keeps omp running and stops it when the session closes.
            pane.TerminalView = (OmpTerminalView)ViewMapping.Create(terminal);
            pane.SteerView = new TerminalSteerView { DataContext = viewModel };
            pane.SteerView.StatusMessage += (_, e) => ReportFor(pane, e.Message);
        }
        pane.ConfirmBar.StopConfirmed += (_, _) => pane.Composer.StopResponse();
        pane.ConfirmBar.CloseConfirmed += (_, _) =>
        {
            viewModel.CloseCommand.Execute(null);
            _rail.FocusRows();
        };
        pane.ConfirmBar.Dismissed += (_, e) => OnConfirmBarDismissed(pane, e.Kind);
        return pane;
    }

    private void AttachRuntime(SessionPane pane, AgentSessionRuntime runtime)
    {
        pane.RunControls = new RunControlsView { DataContext = runtime.RunControls };
        pane.Composer.Runs = runtime.Runs;
        void OnFeedback(object? sender, EventArgs e)
        {
            if (ReferenceEquals(_current, pane))
            {
                RefreshFeedback();
            }
        }

        runtime.FeedbackChanged += OnFeedback;
        pane.Detach += () => runtime.FeedbackChanged -= OnFeedback;
    }

    private static void AttachRunSummary(SessionPane pane, AgentSessionViewModel viewModel)
    {
        void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(AgentSessionViewModel.RunSummaryText), StringComparison.Ordinal))
            {
                pane.Strip.RunSummary = viewModel.RunSummaryText;
            }
        }

        pane.Strip.RunSummary = viewModel.RunSummaryText;
        viewModel.PropertyChanged += OnSessionChanged;
        pane.Detach += () => viewModel.PropertyChanged -= OnSessionChanged;
    }

    private void ReportFor(SessionPane pane, string text)
    {
        if (pane.Runtime is { } runtime)
        {
            runtime.ReportStatus(text);
        }
        else if (ReferenceEquals(pane, _current))
        {
            ShowStatus(text);
        }
    }

    private void OnConfirmBarDismissed(SessionPane pane, ConfirmBarKind kind)
    {
        if (!ReferenceEquals(pane, _current))
        {
            return;
        }

        if (kind == ConfirmBarKind.Stop)
        {
            pane.Composer.FocusPrompt();
        }
        else
        {
            pane.Header.FocusCloseButton();
        }
    }

    private void OnNewAgentCompleted(object? sender, EventArgs e) => FocusThread();

    private void OnRowActivated(object? sender, EventArgs e) => FocusThread();

    // Delete on a row and the menu's Close agent land here. A running agent asks first; a quiet one closes at once.
    private void OnRailCloseRequested(object? sender, AgentRowEventArgs e)
    {
        var sessionId = e.SessionId;
        if (!string.Equals(_panel.Selected?.Id, sessionId, StringComparison.Ordinal))
        {
            _ = CloseUnselectedAsync(sessionId);
            return;
        }

        if (_current is not { } pane)
        {
            return;
        }

        if (pane.ViewModel.RequiresCloseConfirmation)
        {
            pane.ConfirmBar.OpenClose();
        }
        else
        {
            pane.ViewModel.CloseCommand.Execute(null);
        }
    }

    private async Task CloseUnselectedAsync(string sessionId)
    {
        try
        {
            await _panel.CloseSessionAsync(sessionId, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            SetStatus($"{FaultedStatus} {fault.GetBaseException().Message}");
        }
    }

    private void OnSkipLinkClick(object? sender, RoutedEventArgs e)
    {
        if (_kanbanShown)
        {
            FocusBoard();
            return;
        }

        if (AgentsToggle.IsChecked == true)
        {
            // The Agents view stands in for the transcript while it is open, so the skip link lands on its status filter.
            (AgentsHost.Content as Control)?.FindControl<ComboBox>("StatusFilter").FocusWhenReady();
            return;
        }

        if (_openTool is not null)
        {
            // A workspace tool stands in for the transcript while it is open, so the skip link lands on its first control.
            FocusFirst(ToolHost.Content as Control);
            return;
        }

        if (_current is { } pane)
        {
            if (pane.TerminalView is null)
            {
                pane.Transcript.FocusTranscript();
            }
            else
            {
                FocusThread();
            }

            return;
        }

        FocusThread();
    }

    // Keyboard map (UI-SPEC, Keyboard Map). The handler runs in the tunnel phase, so it works from inside text boxes.
    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var control = (e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.None;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != KeyModifiers.None;
        var alt = (e.KeyModifiers & KeyModifiers.Alt) != KeyModifiers.None;
        if (alt)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.N when control && !shift:
                e.Handled = true;
                _ = StartNewAgentAsync();
                break;
            case Key.T when control && shift:
                e.Handled = true;
                _ = StartNewTerminalAsync();
                break;
            case Key.K when control && shift && _boardView is not null:
                e.Handled = true;
                ShowKanbanTab();
                break;
            case Key.A when control && shift && _boardView is not null:
                e.Handled = true;
                ShowAgentsTab();
                FocusThread();
                break;
            case Key.D1 when control && !shift:
                e.Handled = true;
                ShowAgentsTab();
                FocusRegion(RailRegionIndex);
                break;
            case Key.D2 when control && !shift:
                e.Handled = true;
                ShowAgentsTab();
                ShowThread();
                FocusThread();
                break;
            case Key.OemComma when control && !shift:
                e.Handled = true;
                RequestSettings();
                break;
            case Key.F6 when !control:
                e.Handled = true;
                CycleRegions(backwards: shift);
                break;
            case Key.F when control && !shift:
                e.Handled = true;
                OpenSearch();
                break;
            case Key.Escape when !control && !shift:
                e.Handled = DismissWithEscape();
                break;
            default:
                break;
        }
    }

    // Esc dismisses an open confirmation bar, and the transcript search. Esc in the prompt, in the filter box and elsewhere is left to those controls.
    private bool DismissWithEscape()
    {
        if (_current is not { } pane)
        {
            return false;
        }

        if (pane.ConfirmBar.IsStopOpen || pane.ConfirmBar.IsCloseOpen)
        {
            var kind = pane.ConfirmBar.IsStopOpen ? ConfirmBarKind.Stop : ConfirmBarKind.Close;
            pane.ConfirmBar.Dismiss();
            OnConfirmBarDismissed(pane, kind);
            return true;
        }

        if (pane.SearchOpen && IsFocusWithin(SearchHost))
        {
            CloseSearch(pane);
            return true;
        }

        return false;
    }

    private async Task StartNewAgentAsync()
    {
        ShowAgentsTab();
        ShowThread();
        await _panel.NewSessionCommand.ExecuteAsync(null).ConfigureAwait(true);
        FocusThread();
    }

    private async Task StartNewTerminalAsync()
    {
        ShowAgentsTab();
        ShowThread();
        await _panel.NewTerminalCommand.ExecuteAsync(null).ConfigureAwait(true);
        FocusThread();
    }

    // The top-level tabs. Selecting the Kanban tab hides the rail and the thread, which keep their state, and shows the board in their place.
    private void SetKanbanShown(bool shown)
    {
        if (shown && _boardView is null)
        {
            return;
        }

        _kanbanShown = shown;
        _syncingTabs = true;
        try
        {
            TopTabs.SelectedItem = shown ? KanbanTab : AgentsTab;
        }
        finally
        {
            _syncingTabs = false;
        }

        RailRegion.IsVisible = !shown;
        ThreadRegion.IsVisible = !shown;
        KanbanHost.IsVisible = shown;
    }

    private void ShowAgentsTab() => SetKanbanShown(shown: false);

    // Shows the Kanban tab and puts focus on the card of a session, or on the board when no session is named or it has no card.
    private void ShowKanbanTab(string? sessionId = null)
    {
        if (_boardView is null)
        {
            return;
        }

        SetKanbanShown(shown: true);
        if (sessionId is not null && _boardView.CardViewOf(sessionId) is { } card)
        {
            card.FocusWhenReady();
            return;
        }

        FocusBoard();
    }

    private void FocusBoard()
    {
        if (_boardView is null || _boardView.FocusCards())
        {
            return;
        }

        if (_boardView.StartSession.IsEffectivelyVisible)
        {
            _boardView.StartSession.FocusWhenReady();
            return;
        }

        KanbanTab.FocusWhenReady();
    }

    // The user chose a tab with the arrow keys or the pointer. The tab keeps the focus.
    private void OnTopTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncingTabs)
        {
            SetKanbanShown(ReferenceEquals(TopTabs.SelectedItem, KanbanTab));
        }
    }

    // A card was activated: the session is selected in the panel, the Agents tab comes back and focus goes to the session's thread.
    private void OnBoardOpenSessionRequested(object? sender, SessionSelectionChangedEventArgs e)
    {
        if (e.SessionId is not { } sessionId)
        {
            return;
        }

        ShowAgentsTab();
        ShowThread();
        _ = _panel.SelectSession(sessionId);
        FocusThread();
    }

    // The empty board's start button runs the same command as New agent.
    private void OnBoardStartSessionRequested(object? sender, EventArgs e) => _ = StartNewAgentAsync();

    private void OpenSearch()
    {
        if (_current is not { } pane || pane.TerminalView is not null || _kanbanShown || _openTool is not null || AgentsToggle.IsChecked == true)
        {
            return;
        }

        pane.SearchOpen = true;
        SearchHost.IsVisible = true;
        pane.SearchView.FindControl<TextBox>("SearchInput").FocusWhenReady();
    }

    private void CloseSearch(SessionPane pane)
    {
        pane.SearchOpen = false;
        SearchHost.IsVisible = false;
        pane.Transcript.FocusTranscript();
    }

    private int FocusedRegion()
    {
        if (IsFocusWithin(RailRegion))
        {
            return RailRegionIndex;
        }

        return IsFocusWithin(ThreadRegion) ? ThreadRegionIndex : -1;
    }

    private void CycleRegions(bool backwards)
    {
        if (_kanbanShown)
        {
            // The Kanban tab has two regions: the tabs and the board.
            if (IsFocusWithin(TopTabs))
            {
                FocusBoard();
            }
            else
            {
                KanbanTab.FocusWhenReady();
            }

            return;
        }

        int[] regions = [RailRegionIndex, ThreadRegionIndex];
        var at = Array.IndexOf(regions, FocusedRegion());
        int next;
        if (at < 0)
        {
            next = backwards ? regions.Length - 1 : 0;
        }
        else
        {
            next = (at + (backwards ? regions.Length - 1 : 1)) % regions.Length;
        }

        FocusRegion(regions[next]);
    }

    private void FocusRegion(int region)
    {
        switch (region)
        {
            case RailRegionIndex:
                if (ToolsViewScroll.IsVisible)
                {
                    AgentsToggle.FocusWhenReady();
                }
                else
                {
                    _rail.FocusRows();
                }

                break;
            default:
                FocusThread();
                break;
        }
    }

    // The prompt of the selected agent. A closed or lost agent has no prompt, so focus lands on its transcript. With no agent, it lands on
    // the first control of the empty thread.
    private void FocusThread()
    {
        if (_current is { } pane)
        {
            if (pane.TerminalView is { } terminalView)
            {
                terminalView.FindControl<Control>("Output").FocusWhenReady();
            }
            else if (ComposerHost.IsVisible)
            {
                pane.Composer.FocusPrompt();
            }
            else
            {
                pane.Transcript.FocusTranscript();
            }

            return;
        }

        _stateView.FindControl<Button>("StartFirstAgentButton").FocusWhenReady();
    }

    private void OnLayoutSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= WideLayoutWidth;
        if (!_sized || wide != _wide)
        {
            // Crossing the width threshold switches the rail between its two widths.
            _sized = true;
            _wide = wide;
            RailRegion.Classes.Set(WideClass, wide);
        }

        var hint = e.NewSize.Height >= ShortWindowHeight;
        if (hint != _hintVisible)
        {
            _hintVisible = hint;
            if (_current is { } pane)
            {
                pane.Composer.HintVisible = hint;
            }
        }
    }

    private bool IsFocusWithin(Control region)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is not null && (ReferenceEquals(focused, region) || region.IsVisualAncestorOf(focused));
    }

    private void OnRailViewToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingRail || sender is not ToggleButton toggle)
        {
            return;
        }

        // The pair is exclusive: checking one unchecks the other, and the checked one cannot be unchecked.
        SetRailView(tools: ReferenceEquals(toggle, ToolsViewToggle));
    }

    private void SetRailView(bool tools)
    {
        _syncingRail = true;
        try
        {
            SessionsViewToggle.IsChecked = !tools;
            ToolsViewToggle.IsChecked = tools;
        }
        finally
        {
            _syncingRail = false;
        }

        RailHost.IsVisible = !tools;
        ToolsViewScroll.IsVisible = tools;
    }

    private void OnAgentsToggleChanged(object? sender, RoutedEventArgs e)
    {
        var open = AgentsToggle.IsChecked == true;
        if (open)
        {
            CloseTool();
            ToolTitle.Text = "Agents";
            ToolHeader.IsVisible = true;
            _openedBy = AgentsToggle;
        }
        else if (ReferenceEquals(_openedBy, AgentsToggle) && _openTool is null)
        {
            ToolHeader.IsVisible = false;
        }

        AgentsHost.IsVisible = open;
        TranscriptHost.IsVisible = !open;
    }

    private void OnSideChatsToggleChanged(object? sender, RoutedEventArgs e) =>
        SideChatHost.IsVisible = SideChatsToggle.IsChecked == true;

    private void OnSuggestionsToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_current is { } pane)
        {
            pane.Composer.SuggestionsShown = SuggestionsToggle.IsChecked == true;
        }
    }

    // The Close tool control of the tool header closes the open tool, or the Agents view, and returns focus to the toggle that opened it.
    private void OnCloseToolClick(object? sender, RoutedEventArgs e)
    {
        var opener = _openedBy;
        ShowThread();
        if (opener is null)
        {
            return;
        }

        // The toggles live in the Tools view of the rail, so the rail shows it again when the user switched to the session list.
        SetRailView(tools: true);
        opener.FocusWhenReady();
    }

    // Shows the thread: the open tool and the Agents view close and the transcript returns.
    private void ShowThread()
    {
        if (AgentsToggle.IsChecked == true)
        {
            AgentsToggle.IsChecked = false;
        }

        CloseTool();
    }

    // The workspace tools, in the order their toggles sit in the XAML. Each tool's view is built when its toggle first opens it.
    private List<ToolEntry> CreateTools()
    {
        List<ToolEntry> tools =
        [
            _editorTool,
            new ToolEntry(ExplorerToggle, "Explorer", CreateExplorer, keepView: true),
            _terminalTool = new ToolEntry(TerminalToggle, "Terminal", () => new TerminalPanelView(_workspace.FullPath, TerminalSettingsProvider?.Invoke()), keepView: true),
            new ToolEntry(DiffToggle, "Diff review", () => new DiffReviewView(), keepView: true),
            new ToolEntry(InlineEditToggle, "Inline edit", () => new InlineEditView(), keepView: true),
            new ToolEntry(ConventionsToggle, "Conventions", CreateConventions, keepView: true),
            new ToolEntry(McpToggle, "MCP tools", CreateMcpTools, keepView: true),
            new ToolEntry(WorkspaceSearchToggle, "Workspace search", CreateWorkspaceSearch, keepView: true),
            new ToolEntry(ExportToggle, "Transcript export", CreateExport, keepView: false),
        ];

        foreach (var tool in tools)
        {
            tool.Toggle.IsCheckedChanged += OnToolToggleChanged;
        }

        return tools;
    }

    private void OnToolToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingTools || sender is not ToggleButton toggle)
        {
            return;
        }

        var tool = _tools.Single(entry => ReferenceEquals(entry.Toggle, toggle));
        if (toggle.IsChecked == true)
        {
            OpenTool(tool);
        }
        else if (ReferenceEquals(_openTool, tool))
        {
            CloseTool();
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e) => RequestSettings();

    // A kept terminal panel holds the settings it was built with, so a changed setting rebuilds it, but only while it holds no proposal and runs no command.
    private void RebuildTerminalIfIdle()
    {
        if (!_terminalSettingsStale || _terminalTool is not { } tool)
        {
            return;
        }

        if (tool.View is not TerminalPanelView view)
        {
            _terminalSettingsStale = false;
            return;
        }

        var busy = view.FindControl<Button>("StopButton")?.IsEnabled != false || view.FindControl<Button>("ApproveButton")?.IsEnabled != false;
        if (busy)
        {
            return;
        }

        _terminalSettingsStale = false;
        tool.View = null;
        if (ReferenceEquals(_openTool, tool))
        {
            tool.View = tool.Create();
            ToolHost.Content = tool.View;
        }
    }

    // Shows a tool in place of the transcript. A kept view is shown again from the same instance, so an open terminal keeps its
    // output. A tool whose view cannot be built is not shown, and the status line says why.
    private void OpenTool(ToolEntry tool)
    {
        Control view;
        try
        {
            if (ReferenceEquals(tool, _terminalTool))
            {
                RebuildTerminalIfIdle();
            }

            view = tool.KeepView ? (tool.View ??= tool.Create()) : tool.Create();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or WorkspacePathException)
        {
            SetToolChecked(tool, isChecked: false);
            SetStatus($"{ToolFailedStatus} {tool.Name}. {exception.Message}");
            return;
        }

        _syncingTools = true;
        try
        {
            foreach (var other in _tools)
            {
                other.Toggle.IsChecked = ReferenceEquals(other, tool);
            }
        }
        finally
        {
            _syncingTools = false;
        }

        // Closing the Agents view first shows the transcript, which the tool then replaces.
        AgentsToggle.IsChecked = false;
        _openTool = tool;
        _openedBy = tool.Toggle;
        ToolTitle.Text = tool.Name;
        ToolHeader.IsVisible = true;
        ToolHost.Content = view;
        ToolHost.IsVisible = true;
        TranscriptHost.IsVisible = false;
    }

    // Hides the open tool and shows the transcript. A kept view stays alive in its ToolEntry while it is hidden.
    private void CloseTool()
    {
        if (_openTool is null)
        {
            return;
        }

        _openTool = null;
        _syncingTools = true;
        try
        {
            foreach (var tool in _tools)
            {
                tool.Toggle.IsChecked = false;
            }
        }
        finally
        {
            _syncingTools = false;
        }

        ToolHeader.IsVisible = false;
        ToolHost.Content = null;
        ToolHost.IsVisible = false;
        TranscriptHost.IsVisible = true;
    }

    private void SetToolChecked(ToolEntry tool, bool isChecked)
    {
        _syncingTools = true;
        try
        {
            tool.Toggle.IsChecked = isChecked;
        }
        finally
        {
            _syncingTools = false;
        }
    }

    // A file the explorer activates is read from the workspace into the editor, and the editor opens in its place.
    private void OnFileOpenRequested(object? sender, WorkspaceFileRequestedEventArgs e)
    {
        var entry = e.Entry;
        try
        {
            var text = File.ReadAllText(_workspace.Resolve(entry.RelativePath));
            _editorTabs.Open(entry.RelativePath, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or WorkspacePathException)
        {
            SetStatus($"{ToolFailedStatus} {entry.Name}. {exception.Message}");
            return;
        }

        OpenTool(_editorTool);
    }

    private WorkspaceTreeView CreateExplorer()
    {
        _explorer ??= OpenWorkspaceTree();
        var view = (WorkspaceTreeView)ViewMapping.Create(_explorer);
        view.FileOpenRequested += OnFileOpenRequested;
        return view;
    }

    private WorkspaceTree OpenWorkspaceTree()
    {
        var tree = new WorkspaceTree();
        tree.OpenFolder(new LocalWorkspaceFileSystem(_workspace.FullPath));
        return tree;
    }

    private Control CreateConventions()
    {
        _catalog ??= new WorkspaceCatalog(_workspace);
        _conventions ??= new WorkspaceConventionsModel(_catalog);
        return ViewMapping.Create(_conventions);
    }

    // The shell connects no MCP server, so the registry holds no tools until one is connected.
    private Control CreateMcpTools()
    {
        _mcpHost ??= new McpToolRegistrySource(_mcpRegistry ??= new McpToolRegistry());
        _mcpTools ??= new McpToolsModel(_mcpHost);
        return ViewMapping.Create(_mcpTools);
    }

    private Control CreateWorkspaceSearch()
    {
        _workspaceSearch ??= new WorkspaceSearchModel(new WorkspaceSearch(_workspace));
        return ViewMapping.Create(_workspaceSearch);
    }

    // The export view reads the transcript it was given, so it is built again on each open rather than kept.
    private TranscriptExportView CreateExport() =>
        new((_current?.ViewModel.Session ?? new SessionViewModel()).Tree, ExportTitle, new UsageHistory([]));

    private static void FocusFirst(Control? root)
    {
        var target = root?.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled);
        _ = target?.Focus();
    }

    /// <summary>
    /// Releases the view's subscriptions, its transcript searches and the workspace models. The host calls this when its window closes.
    /// It does not stop the sessions: the head owns the panel and the runtimes, and disposes them.
    /// </summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _panel.PropertyChanged -= OnPanelChanged;
        _panel.Catalog.PropertyChanged -= OnCatalogChanged;
        if (_board is not null)
        {
            _board.OpenSessionRequested -= OnBoardOpenSessionRequested;
            _board.StartSessionRequested -= OnBoardStartSessionRequested;
            TopTabs.SelectionChanged -= OnTopTabChanged;
        }

        if (_watched is not null)
        {
            _watched.PropertyChanged -= OnSelectedViewModelChanged;
            _watched = null;
        }

        foreach (var pane in _panes.Values)
        {
            pane.Dispose();
        }

        _panes.Clear();
        _current = null;
        EditorModel.Dispose();
        _conventions?.Dispose();
        _catalog?.Dispose();
        if (_mcpHost is not null)
        {
            _ = _mcpHost.DisposeAsync().AsTask().ContinueWith(
                closed => Trace.TraceError($"OmpOS could not close the MCP tools: {closed.Exception!.GetBaseException()}"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }

    /// <summary>One workspace tool: its toggle, its accessible name, how its view is built, and the view once it is built.</summary>
    private sealed class ToolEntry(ToggleButton toggle, string name, Func<Control> create, bool keepView)
    {
        /// <summary>Gets the toggle that opens the tool.</summary>
        public ToggleButton Toggle { get; } = toggle;

        /// <summary>Gets the tool's name, used in the status line when it cannot open.</summary>
        public string Name { get; } = name;

        /// <summary>Gets whether the view is kept after the tool closes, rather than built again on the next open.</summary>
        public bool KeepView { get; } = keepView;

        /// <summary>Builds the tool's view.</summary>
        public Func<Control> Create { get; } = create;

        /// <summary>Gets the kept view, or null until the tool first opens.</summary>
        public Control? View { get; set; }
    }

    /// <summary>The views and models of one session: they are built when the session is first shown and kept until the session leaves the catalog.</summary>
    private sealed class SessionPane(AgentSessionViewModel viewModel, AgentSessionRuntime? runtime) : IDisposable
    {
        /// <summary>Gets the session view model the pane shows.</summary>
        public AgentSessionViewModel ViewModel { get; } = viewModel;

        /// <summary>Gets the runtime that runs the session, or null when the view model was not built by one.</summary>
        public AgentSessionRuntime? Runtime { get; } = runtime;

        /// <summary>Gets the transcript.</summary>
        public required SessionView Transcript { get; init; }

        /// <summary>Gets the composer.</summary>
        public required ComposerView Composer { get; init; }

        /// <summary>Gets the thread header.</summary>
        public required ThreadHeaderView Header { get; init; }

        /// <summary>Gets the stop and close confirmation bars.</summary>
        public required ConfirmBarView ConfirmBar { get; init; }

        /// <summary>Gets the pipeline strip.</summary>
        public required PipelineStripView Strip { get; init; }

        /// <summary>Gets the transcript search of the session.</summary>
        public required TranscriptSearchModel Search { get; init; }

        /// <summary>Gets the side chats of the session.</summary>
        public required SideChatPanel SideChats { get; init; }

        /// <summary>Gets or sets the transcript search view.</summary>
        public TranscriptSearchView SearchView { get; set; } = null!;

        /// <summary>Gets or sets the side chat view.</summary>
        public Control SideChatView { get; set; } = null!;

        /// <summary>Gets or sets the tool-approval list, or null when the session has no runtime.</summary>
        public RunControlsView? RunControls { get; set; }

        /// <summary>Gets or sets whether the transcript search is open for this session.</summary>
        public bool SearchOpen { get; set; }

        /// <summary>Gets or sets the omp terminal view of a terminal session, or null for a chat. It is kept while another tab or session is shown.</summary>
        public OmpTerminalView? TerminalView { get; set; }

        /// <summary>Gets or sets the steer-only composer of a terminal session, or null for a chat.</summary>
        public TerminalSteerView? SteerView { get; set; }

        /// <summary>Gets or sets the action that unsubscribes the pane from its runtime.</summary>
        public Action? Detach { get; set; }

        /// <summary>Unsubscribes the pane from its runtime and disposes the search.</summary>
        public void Dispose()
        {
            Detach?.Invoke();
            Detach = null;
            TerminalView?.Release();
            Search.Dispose();
        }
    }
}

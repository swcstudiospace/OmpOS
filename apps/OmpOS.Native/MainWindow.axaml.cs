using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Harness.Mcp;
using OmpOS.Native.Engine;
using OmpOS.Native.Views;
using OmpOS.Native.Views.Diff;
using OmpOS.Native.Views.Editor;
using OmpOS.Native.Views.Explorer;
using OmpOS.Native.Views.Export;
using OmpOS.Native.Views.InlineEditing;
using OmpOS.Native.Views.Mcp;
using OmpOS.Native.Views.RunControls;
using OmpOS.Native.Views.Search;
using OmpOS.Native.Views.Terminal;
using OmpOS.Native.Views.Workspace;
using OmpOS.Presentation;
using OmpOS.Presentation.Agents;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Chats;
using OmpOS.Presentation.Components;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Export;
using OmpOS.Presentation.Runs;
using OmpOS.Presentation.State;
using OmpOS.Workspace;

namespace OmpOS.Native;

/// <summary>
/// The one main window of the native head. It hosts the session list, the transcript (or the Agents view or a workspace tool that
/// opens in its place), the status line, the run controls (pending tool approvals), the composer, the transcript search, the side
/// chats of the session, and the workspace tools: the editor, the explorer, the terminal, the diff review, the inline edit, the
/// workspace conventions, the MCP tools, the workspace search and the transcript export. It binds the presentation view models the
/// web head binds. It has no dialog and no second window.
/// </summary>
/// <remarks>
/// The workspace is the folder the window was given. Its models are created when a tool first opens and kept until the window
/// closes, so a window that opens no tool reads no folder and starts no process.
/// </remarks>
internal partial class MainWindow : Window
{
    /// <summary>The status line text after a send the host refuses (UI-SPEC, Copywriting Contract).</summary>
    public const string RefusedSendStatus = "Message not sent. Sending is unavailable.";

    /// <summary>The status line text after Stop with no response running (UI-SPEC, Copywriting Contract).</summary>
    public const string NoResponseStatus = "No response is running.";

    /// <summary>The status line text prefix after a composer action faults. The fault's own message follows it.</summary>
    public const string FaultedStatus = "Something went wrong.";

    /// <summary>The status line text prefix after a workspace tool cannot open. The tool's name and the reason follow it.</summary>
    public const string ToolFailedStatus = "Could not open";

    private const string ReducedMotionClass = "reduced-motion";

    /// <summary>The identifier of the one session this shell shows. The shell has no session transport yet, so the id is fixed.</summary>
    private const string SessionId = "shell-session";

    /// <summary>The level-one heading of a transcript exported from the shell.</summary>
    private const string ExportTitle = "OmpOS session";

    private readonly WorkspaceRoot _workspace;
    private readonly EditorTabs _editorTabs = new();
    private readonly ToolEntry _editorTool;
    private readonly List<ToolEntry> _tools;
    private ToolEntry? _openTool;
    private bool _syncingTools;
    private WorkspaceTree? _explorer;
    private WorkspaceCatalog? _catalog;
    private WorkspaceConventionsModel? _conventions;
    private WorkspaceSearchModel? _workspaceSearch;
    private McpToolRegistry? _mcp;
    private McpToolsModel? _mcpTools;

    /// <summary>
    /// Creates the window with the production defaults: no approval service and no run input are connected in this build, so
    /// every decision and steer fails visibly and nothing is allowed.
    /// </summary>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    public MainWindow(MotionPreference motion)
        : this(new UnconnectedToolApprovalBridge(), new UnconnectedRunInput(), motion)
    {
    }

    /// <summary>
    /// Creates the window with the approval bridge and the run input it routes decisions and messages through. The workspace is the
    /// current directory.
    /// </summary>
    /// <param name="approvalBridge">The bridge that records each tool-approval decision.</param>
    /// <param name="runInput">The input channel of the active run.</param>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public MainWindow(IToolApprovalBridge approvalBridge, IRunInput runInput, MotionPreference motion)
        : this(approvalBridge, runInput, motion, Environment.CurrentDirectory)
    {
    }

    /// <summary>Creates the window with the approval bridge, the run input and the workspace folder its tools work in.</summary>
    /// <param name="approvalBridge">The bridge that records each tool-approval decision.</param>
    /// <param name="runInput">The input channel of the active run.</param>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    /// <param name="workspaceRoot">The workspace folder. It must exist.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="workspaceRoot"/> does not exist.</exception>
    public MainWindow(IToolApprovalBridge approvalBridge, IRunInput runInput, MotionPreference motion, string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(approvalBridge);
        ArgumentNullException.ThrowIfNull(runInput);
        _workspace = new WorkspaceRoot(workspaceRoot);

        InitializeComponent();

        // Phase 20 has no session transport. The shell starts with a loaded, empty list and a loaded, empty transcript,
        // which are the empty states the UI-SPEC copy describes.
        ConversationList.State = ConversationListState.Loaded;
        ConversationList.Conversations = Array.Empty<ConversationSummary>();

        Session = new SessionViewModel();
        Session.MarkHistoryLoaded();
        TranscriptHost.Content = ViewMapping.Create(Session);

        // The Agents window: one registry of runs and one view model over it. The view opens in place of the transcript.
        AgentRuns = new AgentRunRegistry();
        Agents = new AgentsViewModel(AgentRuns);
        AgentsHost.Content = ViewMapping.Create(Agents);
        AgentsToggle.IsCheckedChanged += OnAgentsToggleChanged;

        // The transcript search: one model per session. It follows the session's transcript and is disposed when the window closes.
        Search = new TranscriptSearchModel(Session);
        Search.MatchSelected += OnMatchSelected;
        SearchHost.Content = ViewMapping.Create(Search);

        // The side chats of this session. Opening one adds messages to the side chat panel only, never to the session's transcript.
        SideChats = new SideChatPanel(SessionId);
        SideChatHost.Content = ViewMapping.Create(SideChats);
        SideChatsToggle.IsCheckedChanged += OnSideChatsToggleChanged;
        SuggestionsToggle.IsCheckedChanged += OnSuggestionsToggleChanged;

        RunControls = new RunControlsViewModel(approvalBridge);
        RunControlsHost.Content = new RunControlsView { DataContext = RunControls };

        Runs = new RunInputScheduler(runInput);
        Composer = new ComposerViewModel(
            _ => RefuseSendAsync(),
            StopIdleAsync);
        var composerView = (ComposerView)ViewMapping.Create(Composer);
        composerView.Runs = Runs;
        composerView.WorkFaulted += OnComposerWorkFaulted;
        ComposerHost.Content = composerView;

        // The editor model is shared by the editor tool and the explorer, which opens files into it.
        EditorModel = new EditorViewModel(_editorTabs);
        _editorTool = new ToolEntry(EditorToggle, "Editor", () => ViewMapping.Create(EditorModel), keepView: true);
        _tools = CreateTools();

        Classes.Set(ReducedMotionClass, IsReducedMotion(motion));
    }

    /// <summary>Gets the session view model the transcript binds.</summary>
    public SessionViewModel Session { get; }

    /// <summary>Gets the composer view model the prompt, send and stop controls bind.</summary>
    public ComposerViewModel Composer { get; }

    /// <summary>Gets the pending tool-approval requests and the coordinator that sends each decision.</summary>
    public RunControlsViewModel RunControls { get; }

    /// <summary>Gets the scheduler that routes steering messages and queued follow-ups to the active run.</summary>
    public RunInputScheduler Runs { get; }

    /// <summary>Gets the registry of the agent runs the Agents window shows.</summary>
    public AgentRunRegistry AgentRuns { get; }

    /// <summary>Gets the Agents window state: the runs that match the status filter and the open run view.</summary>
    public AgentsViewModel Agents { get; }

    /// <summary>Gets the transcript search of the session. It is disposed when the window closes.</summary>
    public TranscriptSearchModel Search { get; }

    /// <summary>Gets the side chats opened from the session.</summary>
    public SideChatPanel SideChats { get; }

    /// <summary>Gets the editor state the editor tool binds. The explorer opens its files here. It is disposed when the window closes.</summary>
    public EditorViewModel EditorModel { get; }

    /// <summary>
    /// Resolves a motion setting to the reduced-motion flag. <see cref="MotionPreference.System"/> resolves to full
    /// motion, because Avalonia 12.1.4 exposes no operating-system reader (D-20-08, OQ-34).
    /// </summary>
    private static bool IsReducedMotion(MotionPreference motion) => motion == MotionPreference.Reduced;

    private Task<bool> RefuseSendAsync()
    {
        SetStatus(RefusedSendStatus);
        return Task.FromResult(false);
    }

    private Task StopIdleAsync()
    {
        SetStatus(NoResponseStatus);
        return Task.CompletedTask;
    }

    private void SetStatus(string text)
    {
        StatusLine.Text = text;
        StatusLine.IsVisible = text.Length > 0;
    }

    private void OnComposerWorkFaulted(object? sender, ComposerWorkFaultedEventArgs e) =>
        SetStatus($"{FaultedStatus} {e.Exception.GetBaseException().Message}");

    private void OnSkipLinkClick(object? sender, RoutedEventArgs e)
    {
        if (AgentsToggle.IsChecked == true)
        {
            // The Agents view stands in for the transcript while it is open, so the skip link lands on its status filter.
            _ = (AgentsHost.Content as Control)?.FindControl<ComboBox>("StatusFilter")?.Focus();
            return;
        }

        if (_openTool is not null)
        {
            // A workspace tool stands in for the transcript while it is open, so the skip link lands on its first control.
            FocusFirst(ToolHost.Content as Control);
            return;
        }

        (TranscriptHost.Content as SessionView)?.FocusTranscript();
    }

    private void OnAgentsToggleChanged(object? sender, RoutedEventArgs e)
    {
        var open = AgentsToggle.IsChecked == true;
        if (open)
        {
            CloseTool();
        }

        AgentsHost.IsVisible = open;
        TranscriptHost.IsVisible = !open;
    }

    private void OnSideChatsToggleChanged(object? sender, RoutedEventArgs e) =>
        SideChatHost.IsVisible = SideChatsToggle.IsChecked == true;

    private void OnSuggestionsToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (ComposerHost.Content is ComposerView composer)
        {
            composer.SuggestionsShown = SuggestionsToggle.IsChecked == true;
        }
    }

    private void OnMatchSelected(object? sender, MatchSelectedEventArgs e) =>
        (TranscriptHost.Content as SessionView)?.ScrollToBlock(e.Match.BlockIndex);

    // The workspace tools, in the order their toggles sit in the XAML. Each tool's view is built when its toggle first opens it.
    private List<ToolEntry> CreateTools()
    {
        List<ToolEntry> tools =
        [
            _editorTool,
            new ToolEntry(ExplorerToggle, "Explorer", CreateExplorer, keepView: true),
            new ToolEntry(TerminalToggle, "Terminal", () => new TerminalPanelView(_workspace.FullPath), keepView: true),
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

    // Shows a tool in place of the transcript. A kept view is shown again from the same instance, so an open terminal keeps its
    // output. A tool whose view cannot be built is not shown, and the status line says why.
    private void OpenTool(ToolEntry tool)
    {
        Control view;
        try
        {
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
        _mcp ??= new McpToolRegistry();
        _mcpTools ??= new McpToolsModel(new McpToolRegistrySource(_mcp));
        return ViewMapping.Create(_mcpTools);
    }

    private Control CreateWorkspaceSearch()
    {
        _workspaceSearch ??= new WorkspaceSearchModel(new WorkspaceSearch(_workspace));
        return ViewMapping.Create(_workspaceSearch);
    }

    // The export view reads the transcript it was given, so it is built again on each open rather than kept.
    private TranscriptExportView CreateExport() => new(Session.Tree, ExportTitle, new UsageHistory([]));

    private static void FocusFirst(Control? root)
    {
        var target = root?.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.Focusable && control.IsEffectivelyVisible && control.IsEffectivelyEnabled);
        _ = target?.Focus();
    }

    /// <summary>Stops the transcript search and the workspace models when the window closes, so they hold no subscription.</summary>
    protected override void OnClosed(EventArgs e)
    {
        Search.MatchSelected -= OnMatchSelected;
        Search.Dispose();
        EditorModel.Dispose();
        _conventions?.Dispose();
        _catalog?.Dispose();
        if (_mcp is not null)
        {
            _ = _mcp.DisposeAsync().AsTask();
        }

        base.OnClosed(e);
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
}

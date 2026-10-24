using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;
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
using OmpOS.Sessions;
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
    private readonly EnginePromptChannel? _prompts;
    private readonly EngineRunInput? _queued;
    private bool _stopRequested;
    private long _nextSeq = 1;

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
    /// <param name="engine">The lazily composed engine the window sends prompts through. Null refuses every send and stop.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="workspaceRoot"/> does not exist.</exception>
    public MainWindow(IToolApprovalBridge approvalBridge, IRunInput runInput, MotionPreference motion, string workspaceRoot, Lazy<EngineComposition>? engine = null)
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
        _prompts = engine is null ? null : new EnginePromptChannel(engine);

        // A composed engine holds the messages the active run accepts behind its turn, so its run input must be the engine's own.
        _queued = engine is null ? null : runInput as EngineRunInput ?? throw new ArgumentException("A composed engine needs the engine's run input.", nameof(runInput));
        Composer = new ComposerViewModel(StartTurnAsync, StopTurnAsync);
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

    /// <summary>
    /// Stops the turn in flight through the composed host. The run then ends: messages held behind it are dropped, not sent.
    /// With no turn running the status says so.
    /// </summary>
    /// <returns>A task that completes when the host has accepted the cancellation.</returns>
    private async Task StopTurnAsync()
    {
        if (_prompts is not { } prompts || !Composer.IsStreaming)
        {
            SetStatus(NoResponseStatus);
            return;
        }

        // The flag is raised before the cancel, so the turn's end sees it. It is lowered again only when the cancel was not accepted.
        _stopRequested = true;
        var accepted = false;
        try
        {
            await prompts.CancelAsync(CancellationToken.None).ConfigureAwait(true);
            accepted = true;
        }
        finally
        {
            if (!accepted)
            {
                _stopRequested = false;
            }
        }
    }

    /// <summary>
    /// Sends the draft to the composed host and streams the turn's events into the transcript. The turn runs in the background, so
    /// the composer clears the draft once the host accepts the send. Without a composed engine the send is refused.
    /// </summary>
    /// <param name="text">The draft text the user sent.</param>
    /// <returns>True when the host accepted the send; false when no engine is composed.</returns>
    private Task<bool> StartTurnAsync(string text)
    {
        if (_prompts is not { } prompts || _queued is not { } queued)
        {
            return RefuseSendAsync();
        }

        // The composer does not send while a response streams. The run stays active until its last turn ends, so a message sent
        // meanwhile steers it or queues behind it.
        Composer.IsStreaming = true;
        Runs.RunStarted();
        var correlationId = Guid.NewGuid().ToString("N");
        EchoUserMessage(text, correlationId);
        var turn = DriveTurnsAsync(prompts, queued, text, correlationId);

        // The turn is not awaited. DriveTurnsAsync shows a turn's fault under that turn's own id, so this observes only what escapes it.
        _ = turn.ContinueWith(
            faulted => Dispatcher.UIThread.Post(() => ShowTurnFault(faulted.Exception!.InnerExceptions[0], correlationId)),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Streams one turn's events into the transcript. A fault that ends the turn is not caught here: <see cref="StartTurnAsync"/>
    /// observes it and shows it inline.
    /// </summary>
    /// <param name="prompts">The send path of the composed engine.</param>
    /// <param name="text">The prompt text.</param>
    /// <param name="correlationId">The correlation identifier of the turn.</param>
    /// <returns>A task that completes when the turn has ended.</returns>
    private async Task StreamTurnAsync(EnginePromptChannel prompts, string text, string correlationId)
    {
        await foreach (var evt in prompts.SubmitAsync(text, CancellationToken.None).ConfigureAwait(true))
        {
            ShowTurnEvent(evt, correlationId);
        }
    }

    /// <summary>
    /// Runs the turns of one run in send order: the sent prompt first, then each message the run accepted behind it, one turn each.
    /// The run ends when no message is held. A stop or a faulted turn ends it early: whatever is still held is dropped and the
    /// transcript says how many were not sent.
    /// </summary>
    /// <param name="prompts">The send path of the composed engine.</param>
    /// <param name="queued">The run input that holds the messages accepted behind the active turn.</param>
    /// <param name="text">The prompt the run starts with.</param>
    /// <param name="correlationId">The correlation identifier of the first turn.</param>
    /// <returns>A task that completes when the run has ended.</returns>
    private async Task DriveTurnsAsync(EnginePromptChannel prompts, EngineRunInput queued, string text, string correlationId)
    {
        var turnCorrelationId = correlationId;
        try
        {
            while (true)
            {
                try
                {
                    await StreamTurnAsync(prompts, text, turnCorrelationId).ConfigureAwait(true);
                }
                catch (Exception fault) when (fault is not OperationCanceledException)
                {
                    // The fault ends the run. Its reason is shown under the turn that faulted, and the held messages are dropped in
                    // the finally, so the not-sent line follows the reason.
                    ShowTurnFault(fault, turnCorrelationId);
                    return;
                }

                if (_stopRequested)
                {
                    return;
                }

                // The turn ended on its own. The scheduler dispatches the next follow-up if one waits; the next held message runs next.
                await Runs.RunCompletedAsync(CancellationToken.None).ConfigureAwait(true);
                var next = queued.TakeNext();
                Runs.NotifySteersHeldChanged();
                if (next is null)
                {
                    return;
                }

                text = next;
                turnCorrelationId = Guid.NewGuid().ToString("N");
                EchoUserMessage(text, turnCorrelationId);
                if (!Runs.IsRunActive)
                {
                    Runs.RunStarted();
                }
            }
        }
        finally
        {
            // A run still active here was stopped or its turn faulted. Nothing held is sent after either.
            var notSent = Runs.DiscardQueuedFollowUps() + queued.Discard();
            Runs.NotifySteersHeldChanged();
            if (Runs.IsRunActive)
            {
                await Runs.RunCompletedAsync(CancellationToken.None).ConfigureAwait(true);
            }

            Composer.IsStreaming = false;
            _stopRequested = false;
            if (notSent > 0)
            {
                ShowNotSent(notSent, turnCorrelationId);
            }
        }
    }

    /// <summary>
    /// Echoes a prompt the composed run has just sent into the transcript as the user's own turn. The echo is a shared
    /// <see cref="UserMessagePayload"/> that the presentation layer folds, so it lands ahead of the reply it prompted.
    /// </summary>
    /// <param name="text">The prompt text that was sent.</param>
    /// <param name="correlationId">The correlation identifier of the turn the prompt starts.</param>
    private void EchoUserMessage(string text, string correlationId)
    {
        Session.ApplyEvent(new StreamEvent
        {
            Seq = _nextSeq,
            SessionId = SessionId,
            CorrelationId = correlationId,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = new UserMessagePayload { Text = text },
        });
        _nextSeq++;
    }

    /// <summary>Shows the inline line that says how many held messages were not sent when the run ended.</summary>
    /// <param name="count">The number of messages not sent.</param>
    /// <param name="correlationId">The correlation identifier of the last turn of the run.</param>
    private void ShowNotSent(int count, string correlationId)
    {
        var noun = count == 1 ? "message was" : "messages were";
        var line = new MarkdownBlock { Id = $"not-sent-{correlationId}", Markdown = $"{count} queued {noun} not sent." };
        ShowTurnEvent(new UiBlockEvent(line), correlationId);
    }

    /// <summary>Shows the fault that ended a turn inline with its reason. A cancelled turn is not a fault and adds no row.</summary>
    /// <param name="fault">The exception that ended the turn.</param>
    /// <param name="correlationId">The correlation identifier of the turn.</param>
    private void ShowTurnFault(Exception fault, string correlationId)
    {
        if (fault is OperationCanceledException)
        {
            return;
        }

        ShowTurnEvent(new ErrorEvent(fault.Message, Retryable: false), correlationId);
    }

    /// <summary>
    /// Shows one host event: a permission request becomes a pending approval, and every event folds into the transcript through the
    /// shared harness projection.
    /// </summary>
    /// <param name="evt">The host event.</param>
    /// <param name="correlationId">The correlation identifier of the turn.</param>
    private void ShowTurnEvent(HarnessEvent evt, string correlationId)
    {
        // The id the host's resolver holds the request under is the one the bridge decides by.
        if (evt is PermissionRequestEvent request)
        {
            RunControls.Approvals.Add(new PendingToolApproval(SessionId, request.RequestId ?? request.CallId, request.Name, request.PayloadJson));
        }

        foreach (var streamEvent in HarnessEventProjection.ToStreamEvents(_nextSeq, SessionId, correlationId, DateTimeOffset.UtcNow, evt))
        {
            Session.ApplyEvent(streamEvent);
            _nextSeq = streamEvent.Seq + 1;
        }
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
        if (_prompts is not null)
        {
            _ = CloseEngineAsync(_prompts).ContinueWith(
                closed => Dispatcher.UIThread.Post(() => SetStatus($"{FaultedStatus} {closed.Exception!.GetBaseException().Message}")),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        _conventions?.Dispose();
        _catalog?.Dispose();
        if (_mcp is not null)
        {
            _ = _mcp.DisposeAsync().AsTask();
        }

        base.OnClosed(e);
    }

    /// <summary>
    /// Cancels the turn in flight, if one runs, and then disposes the composed host. The window is closing, so nothing awaits this; a
    /// failure reaches the status line through the caller's continuation.
    /// </summary>
    /// <param name="prompts">The send path of the composed engine.</param>
    /// <returns>A task that completes when the host has been disposed.</returns>
    private async Task CloseEngineAsync(EnginePromptChannel prompts)
    {
        try
        {
            if (Composer.IsStreaming)
            {
                await StopTurnAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            await prompts.DisposeAsync().ConfigureAwait(true);
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
}

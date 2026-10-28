using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using OmpOS.Native.Views.Checkpoints;
using OmpOS.Native.Views.Modes;
using OmpOS.Native.Views.Queue;
using OmpOS.Native.Views.Sessions;
using OmpOS.Native.Views.Shell;
using OmpOS.Native.Views.SideChats;
using OmpOS.Presentation.Checkpoints;
using OmpOS.Presentation.Commands.Handlers;
using OmpOS.Presentation.Modes;
using OmpOS.Presentation.Queue;
using OmpOS.Presentation.SideChats;
using OmpOS.Workspace;

namespace OmpOS.Native;

/// <summary>The per-session wiring of the shell: checkpoints, the queue of messages, custom modes, side chats and forks, hook notices, the remote status of the rail row, and the slash handlers of the session.</summary>
public partial class MainView
{
    // Brings the shell's per-session models and views in. A terminal session has no transcript to checkpoint or queue behind, so it gets none of them.
    private void AttachShellPane(SessionPane pane, AgentSessionRuntime? runtime)
    {
        if (runtime is null || runtime.Terminal is not null)
        {
            return;
        }

        AttachCheckpoints(pane, runtime);
        AttachMode(pane, runtime);
        AttachQueue(pane, runtime);
        AttachSideChats(pane, runtime);
        AttachHookNotices(pane, runtime);
        AttachRemoteStatus(pane, runtime);
    }

    #region Checkpoints

    private void AttachCheckpoints(SessionPane pane, AgentSessionRuntime runtime)
    {
        if (_services.Checkpoints is not { } backend || pane.Changes is null)
        {
            return;
        }

        var viewModel = pane.ViewModel;
        var recorder = new CheckpointRecorderModel(viewModel.Id, runtime.WorkspaceRoot, backend);
        var model = new CheckpointsModel(viewModel.Id, backend, TimeProvider.System);
        pane.CheckpointRecorder = recorder;
        pane.CheckpointsModel = model;
        pane.CheckpointsView = new CheckpointsTimelineView { DataContext = model };
        pane.CheckpointsView.CloseRequested += (_, _) => pane.Composer.FocusPrompt();

        // A rewound turn changed files on disk: the Changes bar reads them again.
        model.Rewound += (_, _) => pane.Changes?.Update(viewModel.Session.Tree.Blocks);
        void OnModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(CheckpointsModel.IsOpen), StringComparison.Ordinal) && ReferenceEquals(pane, _current))
            {
                SyncCheckpointsToggle();
            }
        }

        // Each folded block list is a candidate turn: the recorder cuts the turns and ignores the ones it already holds.
        void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(OmpOS.Presentation.State.SessionViewModel.Tree), StringComparison.Ordinal))
            {
                _ = RecordCheckpointsAsync(pane);
            }
        }

        model.PropertyChanged += OnModelChanged;
        viewModel.Session.PropertyChanged += OnSessionChanged;
        pane.Detach += () =>
        {
            model.PropertyChanged -= OnModelChanged;
            viewModel.Session.PropertyChanged -= OnSessionChanged;
        };
        _ = model.RefreshAsync(CancellationToken.None);
    }

    private static async Task RecordCheckpointsAsync(SessionPane pane)
    {
        if (pane.CheckpointRecorder is not { } recorder || pane.CheckpointsModel is not { } model)
        {
            return;
        }

        var blocks = pane.ViewModel.Session.Tree.Blocks;
        var last = blocks.Count == 0 ? null : blocks[^1];
        if (blocks.Count == pane.RecordedBlockCount && ReferenceEquals(last, pane.RecordedLastBlock))
        {
            return;
        }

        pane.RecordedBlockCount = blocks.Count;
        pane.RecordedLastBlock = last;
        try
        {
            await recorder.UpdateAsync(blocks, CancellationToken.None).ConfigureAwait(true);
            await model.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            Trace.TraceWarning($"OmpOS could not record a checkpoint ({exception.GetType().Name}).");
        }
    }

    #endregion

    #region Queue and modes

    private void AttachMode(SessionPane pane, AgentSessionRuntime runtime)
    {
        if (_services.CreateModeSkills is not { } create)
        {
            return;
        }

        var mode = new CustomModeModel(create(runtime.WorkspaceRoot));
        pane.CustomMode = mode;
        pane.CustomModeView = new CustomModeChipView { DataContext = mode };

        // Every prompt the engine gets (a send, a queued message, a follow-up) carries the pinned mode; slash actions do not.
        runtime.PromptPreamble = mode;
    }

    private void AttachQueue(SessionPane pane, AgentSessionRuntime runtime)
    {
        if (_queues is null || pane.ViewModel.Steering is not { } steering)
        {
            return;
        }

        var queue = _queues.GetOrCreate(pane.ViewModel.Id);
        var adapter = new QueuedMessagesSteeringAdapter(queue, steering, (text, cancellationToken) => runtime.StartTurnAsync(text, cancellationToken));
        pane.Queue = queue;
        pane.QueueAdapter = adapter;
        pane.QueueView = new QueuedMessagesView { DataContext = queue };

        // Tab in the prompt queues the draft while a run is active; Enter keeps steering.
        pane.Composer.QueueRequest = text => queue.Enqueue(text) == QueueOutcome.Queued;

        // The next message leaves the queue when the run ends.
        void OnRunsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(OmpOS.Presentation.Runs.RunInputScheduler.IsRunActive), StringComparison.Ordinal) && !runtime.Runs.IsRunActive)
            {
                _ = DispatchQueuedAsync(pane);
            }
        }

        runtime.Runs.PropertyChanged += OnRunsChanged;
        pane.Detach += () =>
        {
            runtime.Runs.PropertyChanged -= OnRunsChanged;
            pane.Composer.QueueRequest = null;
            _queues?.Remove(pane.ViewModel.Id);
        };
    }

    private async Task DispatchQueuedAsync(SessionPane pane)
    {
        if (pane.QueueAdapter is not { } adapter || pane.Queue is not { IsEmpty: false })
        {
            return;
        }

        try
        {
            await adapter.DispatchNextAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            ReportFor(pane, $"{FaultedStatus} {exception.GetBaseException().Message}");
        }
    }

    #endregion

    #region Side chats, forks, notices, remote status

    private void AttachSideChats(SessionPane pane, AgentSessionRuntime runtime)
    {
        var viewModel = pane.ViewModel;
        if (runtime.Forker is { } forker)
        {
            var fork = new ForkModel(viewModel.Id, forker);
            fork.Forked += OnForked;
            pane.Fork = fork;
            pane.Detach += () => fork.Forked -= OnForked;
        }

        if (runtime.SideChatEngine is not { } engine)
        {
            return;
        }

        var hub = new SideChatHub(pane.SideChats, engine, () => viewModel.Session.Tree.Blocks);
        pane.Hub = hub;
        pane.SideChatView = new SideChatPaneView { DataContext = hub, Fork = pane.Fork };

        // A side chat that opens shows the side chat area.
        void OnHubChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(SideChatHub.Open), StringComparison.Ordinal) && hub.Open.Count > 0 && ReferenceEquals(pane, _current))
            {
                SideChatsToggle.IsChecked = true;
            }
        }

        hub.PropertyChanged += OnHubChanged;
        pane.Detach += () => hub.PropertyChanged -= OnHubChanged;
    }

    // A forked session is in the catalog once the engine set registers it: the rail reads again and selects it.
    private void OnForked(object? sender, ForkedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            _panel.Catalog.Refresh();
            _ = _panel.SelectSession(e.Result.NewSessionId);
            FocusThread();
        });

    private void AttachHookNotices(SessionPane pane, AgentSessionRuntime runtime)
    {
        void OnNotice(object? sender, HookNoticeRaisedEventArgs e) => Dispatcher.UIThread.Post(() => ShowHookNotice(pane, e.Notice));
        void OnReview(object? sender, EventArgs e) => RequestSettings(OmpOS.Presentation.Settings.SettingsSection.Hooks);

        runtime.HookNoticeRaised += OnNotice;
        pane.Transcript.HookReviewRequested += OnReview;
        pane.Detach += () =>
        {
            runtime.HookNoticeRaised -= OnNotice;
            pane.Transcript.HookReviewRequested -= OnReview;
        };
    }

    // The remote connection decides the rail row's words while it is not healthy: Reconnecting, Interrupted, Disconnected.
    private void AttachRemoteStatus(SessionPane pane, AgentSessionRuntime runtime)
    {
        void OnStatus(object? sender, OmpOS.Presentation.Remote.RemoteEnvironmentStatusChangedEventArgs e) =>
            Dispatcher.UIThread.Post(() => _panel.Catalog.SetRemoteStatus(pane.ViewModel.Id, e.Status));

        runtime.RemoteStatusChanged += OnStatus;
        pane.Detach += () => runtime.RemoteStatusChanged -= OnStatus;
    }

    #endregion

    #region Slash handlers

    /// <summary>The handlers of the native slash actions that run through the session's own models and the window's services.</summary>
    private List<ISlashActionHandler> CreateHandlers(SessionPane pane)
    {
        var display = _services.Display;
        var window = () => TopLevel.GetTopLevel(this);
        var session = new SessionSlashContext(pane.ViewModel, () => pane.Runtime?.LastRequestId);
        var clipboard = new ClipboardWriter(window);
        List<ISlashActionHandler> handlers =
        [
            new CopySlashHandler(HandlerActionIds.ChatCopy, session, clipboard),
            new CopySlashHandler(HandlerActionIds.ChatCopyConversationId, session, clipboard),
            new CopySlashHandler(HandlerActionIds.ChatCopyRequestId, session, clipboard),
            new AboutSlashHandler(new HandlerAboutLauncher(CurrentAbout, () => window() as Window)),
            new QuitSlashHandler(new WindowLifetime(window)),
            new OpenFileSlashHandler(new DelegateEditorOpener((path, line, cancellationToken) => OpenInEditorAsync(path, line, cancellationToken))),
            new ChangesSlashHandler(() => pane.ChangesOpener),
            new CheckpointSlashHandler(HandlerActionIds.ChatRewind, () => pane.CheckpointsModel, () => display?.RewindEnabled ?? true),
            new CheckpointSlashHandler(HandlerActionIds.ChatRestore, () => pane.CheckpointsModel, () => display?.RewindEnabled ?? true),
            new CheckpointSlashHandler(HandlerActionIds.ChatUndo, () => pane.CheckpointsModel, () => display?.RewindEnabled ?? true),
            new ForkSlashHandler(() => pane.Fork),
            new SideChatSlashHandler(() => pane.Hub),
            new WorktreeSlashHandler(HandlerActionIds.WorktreeApply, () => _worktrees),
            new WorktreeSlashHandler(HandlerActionIds.WorktreeDelete, () => _worktrees),
            new WorktreeSlashHandler(HandlerActionIds.WorktreeBestOfN, () => _worktrees),
        ];

        if (_services.UrlLauncher is { } urls)
        {
            handlers.Add(new FeedbackSlashHandler(new HandlerUrlLauncher(urls)));
        }

        if (_services.WorkspaceSets is { } store)
        {
            var backend = new WorkspaceSetsBackend(store, () => [pane.Runtime?.WorkspaceRoot ?? _workspace.FullPath], (roots, cancellationToken) => StartOnRootsAsync(roots[0].DisplayName, roots, cancellationToken));
            handlers.Add(new WorkspaceSetSlashHandler(HandlerActionIds.WorkspaceSave, backend));
            handlers.Add(new WorkspaceSetSlashHandler(HandlerActionIds.WorkspaceLoad, backend));
        }

        if (display is not null)
        {
            handlers.Add(new DisplayToggleSlashHandler(HandlerActionIds.DisplayLineNumbers, display));
            handlers.Add(new DisplayToggleSlashHandler(HandlerActionIds.DisplayStatusIndicators, display));
        }

        return handlers;
    }

    // /open path[:line]: the file opens in the editor tool. The editor shows the line when it has the file selected.
    private async Task<bool> OpenInEditorAsync(string workspacePath, int? line, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var text = await File.ReadAllTextAsync(_workspace.Resolve(workspacePath), cancellationToken).ConfigureAwait(true);
            _editorTabs.Open(workspacePath, text);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or WorkspacePathException)
        {
            return false;
        }

        OpenTool(_editorTool);
        if (line is { } number && _editorTool.View is OmpOS.Native.Views.Editor.EditorView editor)
        {
            editor.GoToLine(number);
        }

        return true;
    }

    // Show on /changes: the transcript comes back (a tool or the Agents view closes) and focus lands on the Changes bar.
    private void ShowChangesBar(SessionPane pane)
    {
        if (!ReferenceEquals(pane, _current))
        {
            return;
        }

        ShowThread();
        FocusFirst(pane.ChangesView);
    }

    #endregion
}

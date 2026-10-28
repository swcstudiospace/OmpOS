using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using OmpOS.Native.Views;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Native.Views.Cloud;
using OmpOS.Native.Views.Display;
using OmpOS.Native.Views.Hooks;
using OmpOS.Native.Views.Remote;
using OmpOS.Native.Views.Shell;
using OmpOS.Native.Views.Workspace;
using OmpOS.Native.Views.Worktrees;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Cloud;
using OmpOS.Presentation.Commands;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Hooks;
using OmpOS.Presentation.Queue;
using OmpOS.Presentation.Remote;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Worktrees;
using OmpOS.Workspace.Sets;

namespace OmpOS.Native;

/// <summary>The window-level wiring of the shell: About, the conversation search overlay, the environment picker, cloud rows and hand-off, the workspace sets dialog, hooks and worktrees tools, and the Settings route.</summary>
public partial class MainView
{
    private ShellServices _services = new();
    private IAboutLauncher? _about;
    private ConversationSearchIndex? _searchIndex;
    private ConversationSearchModel? _searchModel;
    private ConversationSearchView? _searchView;
    private IInputElement? _searchReturnFocus;
    private NewSessionTargetModel? _targetModel;
    private EnvironmentPickerView? _targetView;
    private HooksServiceSource? _hooksSource;
    private HookRunLogModel? _hookLog;
    private EventHandler<HookRunItemEventArgs>? _hookRunHandler;
    private WorktreeCommandsModel? _worktrees;
    private WorktreeCommandsView? _worktreesView;
    private QueuedMessagesRegistry? _queues;
    private EventHandler? _cloudChanged;
    private bool _syncingWorktrees;
    private bool _syncingCheckpoints;

    /// <summary>The seam that shows the cloud hand-off dialog. Tests replace it; the default shows <see cref="CloudHandoffWindow"/> over the main window and answers its result, or <see langword="null"/> when it was cancelled.</summary>
    internal Func<CloudHandoffModel, Window?, Task<CloudHandoffResult?>> CloudHandoffDialog { get; set; } = ShowCloudHandoffAsync;

    /// <summary>The seam that shows the workspace sets dialog. Tests replace it; the default shows <see cref="WorkspaceSetPickerWindow"/> and answers whether Start was chosen.</summary>
    internal Func<WorkspaceSetPicker, Window?, Task<bool>> WorkspaceDialog { get; set; } = ShowWorkspaceWindowAsync;

    /// <summary>Gets the conversation search model the Ctrl or Cmd+K overlay shows, or <see langword="null"/> before the shell wiring runs.</summary>
    internal ConversationSearchModel? ConversationSearch => _searchModel;

    /// <summary>Gets the environment picker model of the rail, or <see langword="null"/> in a window with no remote environments host.</summary>
    internal NewSessionTargetModel? TargetModel => _targetModel;

    /// <summary>Gets the worktree commands model of the window, or <see langword="null"/> when worktrees are not wired.</summary>
    internal WorktreeCommandsModel? Worktrees => _worktrees;

    /// <summary>Gets the launcher of the About window: the head's, or this build's own.</summary>
    private IAboutLauncher CurrentAbout => _about ??= _services.About ?? new AboutLauncher();

    /// <summary>
    /// Asks for the Settings window on a page. The host that owns the window handles <see cref="SettingsRequested"/> and reads the section; a null section opens the first page.
    /// </summary>
    /// <param name="section">The page, or <see langword="null"/> for the first.</param>
    public void RequestSettings(SettingsSection? section) => SettingsRequested?.Invoke(this, new SettingsRequestedEventArgs(section));

    // Reads a slash action's settings argument (models, agents, run-mode, ...) as a Settings page. An empty argument is the first page.
    private static bool TryReadSettingsSection(string argument, out SettingsSection? section)
    {
        section = null;
        if (argument.Length == 0)
        {
            return true;
        }

        if (Enum.TryParse<SettingsSection>(argument.Replace("-", string.Empty, StringComparison.Ordinal), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
        {
            section = parsed;
            return true;
        }

        return false;
    }

    // Brings the window-level services in. It runs once from the constructor, after the rail, the tools and the panel exist and before the first pane is built.
    private void InitializeShell()
    {
        DisplayOptions.SetSettings(this, _services.Display);
        AboutButton.Click += OnAboutClick;
        WorkspacesButton.Click += OnWorkspacesClick;
        InitializeSearchOverlay();
        InitializeTargetPicker();
        InitializeHooks();
        InitializeWorktrees();
        InitializeCloud();
        CheckpointsToggle.IsVisible = _services.Checkpoints is not null;
        CheckpointsToggle.IsCheckedChanged += OnCheckpointsToggleChanged;
        _queues = new QueuedMessagesRegistry(TimeProvider.System);
        WorkspacesButton.IsVisible = _services.WorkspacePicker is not null;
    }

    // The tools the shell adds to the Tools list, in the order of their toggles.
    private List<ToolEntry> CreateShellTools()
    {
        List<ToolEntry> tools = [];
        WorktreesToggle.IsVisible = _services.Worktrees is not null;
        HookLogToggle.IsVisible = _services.Hooks is not null;
        if (_services.Worktrees is not null)
        {
            tools.Add(new ToolEntry(WorktreesToggle, "Worktrees", CreateWorktreesView, keepView: true));
        }

        if (_services.Hooks is not null)
        {
            tools.Add(new ToolEntry(HookLogToggle, "Hook run log", CreateHookLogView, keepView: true));
        }

        return tools;
    }

    private void CloseShell()
    {
        AboutButton.Click -= OnAboutClick;
        WorkspacesButton.Click -= OnWorkspacesClick;
        CheckpointsToggle.IsCheckedChanged -= OnCheckpointsToggleChanged;
        if (_searchView is not null)
        {
            _searchView.SessionChosen -= OnSearchSessionChosen;
            _searchView.Dismissed -= OnSearchDismissed;
        }

        if (_targetView is not null)
        {
            _targetView.ManageRequested -= OnManageEnvironmentsRequested;
        }

        if (_targetModel is not null)
        {
            _targetModel.PropertyChanged -= OnTargetModelChanged;
        }

        EnvironmentExpander.PropertyChanged -= OnEnvironmentExpanderChanged;
        if (_hooksSource is not null && _hookRunHandler is not null)
        {
            _hooksSource.RunRecorded -= _hookRunHandler;
        }

        if (_worktrees is not null)
        {
            _worktrees.PropertyChanged -= OnWorktreesChanged;
            WorktreesToggle.IsCheckedChanged -= OnWorktreesToggleChanged;
        }

        if (_services.Cloud is { } cloud && _cloudChanged is not null)
        {
            cloud.Changes -= _cloudChanged;
        }
    }

    private void OnAboutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CurrentAbout.Show(TopLevel.GetTopLevel(this) as Window);

    #region Conversation search

    private void InitializeSearchOverlay()
    {
        _searchIndex = new ConversationSearchIndex();
        _searchModel = new ConversationSearchModel(_searchIndex);
        _searchModel.SyncSessions(_panel.Catalog.Sessions);
        _searchView = new ConversationSearchView { DataContext = _searchModel };
        _searchView.SessionChosen += OnSearchSessionChosen;
        _searchView.Dismissed += OnSearchDismissed;
        ConversationSearchHost.Content = _searchView;
    }

    // Ctrl or Cmd+K: opens the conversation search over the window, or closes it when it is open. Ctrl or Cmd+F stays the find in the open conversation.
    private void ToggleConversationSearch()
    {
        if (_searchModel is null || _searchIndex is null)
        {
            return;
        }

        if (_searchModel.IsOpen)
        {
            _searchModel.Close();
            RestoreSearchFocus();
            return;
        }

        _searchModel.SyncSessions(_panel.Catalog.Sessions);
        IndexTranscripts();
        _searchReturnFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        _searchModel.Open();
    }

    // The search matches the titles of every session and the text of the sessions whose transcript the window has shown.
    private void IndexTranscripts()
    {
        foreach (var pane in _panes.Values)
        {
            _searchIndex!.Upsert(pane.ViewModel.Id, pane.ViewModel.Title, ConversationSearchTranscriptText.From(pane.ViewModel.Session.Tree));
        }
    }

    private void OnSearchSessionChosen(object? sender, SessionSelectionChangedEventArgs e)
    {
        if (e.SessionId is not { } sessionId)
        {
            return;
        }

        _searchModel?.Close();
        _searchReturnFocus = null;
        ShowAgentsTab();
        ShowThread();
        _ = _panel.SelectSession(sessionId);
        FocusThread();
    }

    private void OnSearchDismissed(object? sender, EventArgs e)
    {
        _searchModel?.Close();
        RestoreSearchFocus();
    }

    private void RestoreSearchFocus()
    {
        var target = _searchReturnFocus;
        _searchReturnFocus = null;
        if (target is Control control && control.IsEffectivelyVisible)
        {
            control.FocusWhenReady();
            return;
        }

        FocusThread();
    }

    #endregion

    #region Environment picker

    private void InitializeTargetPicker()
    {
        if (_services.RemoteEnvironments is null)
        {
            return;
        }

        _targetModel = new NewSessionTargetModel([]);
        _targetView = new EnvironmentPickerView { DataContext = _targetModel };
        _targetView.ManageRequested += OnManageEnvironmentsRequested;
        _targetModel.PropertyChanged += OnTargetModelChanged;
        EnvironmentHost.Content = _targetView;
        EnvironmentExpander.IsVisible = true;
        EnvironmentExpander.PropertyChanged += OnEnvironmentExpanderChanged;
        RefreshTargetHeader();
        _ = LoadTargetsAsync();
    }

    private async Task LoadTargetsAsync()
    {
        if (_services.RemoteEnvironments is not { } host || _targetModel is null)
        {
            return;
        }

        try
        {
            var list = await host.LoadProfilesAsync(CancellationToken.None).ConfigureAwait(true);
            _targetModel.SetProfiles(list.Profiles.Select(static stored => stored.Profile));
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            Trace.TraceWarning($"OmpOS could not read the remote environments ({exception.GetType().Name}).");
        }
    }

    // Opening the picker reads the profiles again, so an environment saved in Settings shows at once.
    private void OnEnvironmentExpanderChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Expander.IsExpandedProperty && EnvironmentExpander.IsExpanded)
        {
            _ = LoadTargetsAsync();
        }
    }

    private void OnTargetModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RefreshTargetHeader();
        ApplyPendingTarget();
        UpdateNewSessionGate();
    }

    private void OnManageEnvironmentsRequested(object? sender, EventArgs e) => RequestSettings(SettingsSection.Remote);

    private void RefreshTargetHeader()
    {
        var label = _targetModel?.Selected?.Label ?? RemoteCopy.LocalLabel;
        EnvironmentExpander.Header = RemoteCopy.PickerHeading + ": " + label;
        Avalonia.Automation.AutomationProperties.SetName(EnvironmentExpander, RemoteCopy.PickerHeading + ": " + label);
    }

    // The next new session starts on the picker's choice. The engine set takes the pending target when it creates a session, so it is set again after every start.
    private void ApplyPendingTarget()
    {
        if (_services.NewSessionTarget is not { } pending || _targetModel is null)
        {
            return;
        }

        pending.Set(_targetModel.TryGetTarget(out var target) ? target : null);
    }

    // A choice that cannot start (an environment that is blocked or incomplete) disables New agent, and the picker's message says why.
    private void UpdateNewSessionGate()
    {
        if (_targetModel is null)
        {
            return;
        }

        if (_rail.FindControl<Button>("NewAgentButton") is { } button)
        {
            button.IsEnabled = _targetModel.CanStart;
        }
    }

    private bool NewSessionBlocked(out string reason)
    {
        reason = _targetModel is { CanStart: false } model ? model.Validation.Message : string.Empty;
        return reason.Length > 0;
    }

    #endregion

    #region Hooks

    private void InitializeHooks()
    {
        if (_services.Hooks is not { } hooks)
        {
            return;
        }

        _hooksSource = new HooksServiceSource(hooks, () => new OmpOS.Harness.AgentLoop.Hooks.HooksWorkspace { Roots = [_workspace.FullPath] });
        var log = new HookRunLogModel();
        log.Replace(_hooksSource.RecentRuns(HookRunLogModel.Capacity));
        _hookLog = log;
        _hookRunHandler = (_, e) => Dispatcher.UIThread.Post(() => log.Append(e.Run));
        _hooksSource.RunRecorded += _hookRunHandler;
    }

    private HookRunLogDrawerView CreateHookLogView()
    {
        var drawer = new HookRunLogDrawerView { DataContext = _hookLog };
        drawer.CloseRequested += (_, _) => OnCloseToolClick(drawer, new Avalonia.Interactivity.RoutedEventArgs());
        return drawer;
    }

    // A hook notice names the hook and, when a tool call fired it, the call: it hangs under that tool's row, otherwise under the conversation.
    private void ShowHookNotice(SessionPane pane, OmpOS.Harness.AgentLoop.Hooks.HookNotice notice)
    {
        var name = _hooksSource?.NameOf(notice.HookId) ?? notice.HookId ?? string.Empty;
        pane.Transcript.AddHookNotice(HookNoticeRow.From(HooksServiceSource.MapNotice(notice, name)));
    }

    #endregion

    #region Worktrees

    private void InitializeWorktrees()
    {
        if (_services.Worktrees is not { } backend)
        {
            return;
        }

        _worktrees = new WorktreeCommandsModel(backend, TimeProvider.System, _services.BestOfN);
        _worktrees.PropertyChanged += OnWorktreesChanged;
        WorktreesToggle.IsCheckedChanged += OnWorktreesToggleChanged;
    }

    private WorktreeCommandsView CreateWorktreesView()
    {
        _worktreesView = new WorktreeCommandsView { DataContext = _worktrees };
        _worktreesView.CloseRequested += OnWorktreesCloseRequested;
        return _worktreesView;
    }

    // The model's open flag and the Tools toggle are one switch: a slash command that opens the model shows the panel, and closing the panel closes the model.
    private void OnWorktreesChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(WorktreeCommandsModel.IsOpen), StringComparison.Ordinal) || _worktrees is null || _syncingWorktrees)
        {
            return;
        }

        _syncingWorktrees = true;
        try
        {
            if (WorktreesToggle.IsChecked != _worktrees.IsOpen)
            {
                WorktreesToggle.IsChecked = _worktrees.IsOpen;
            }
        }
        finally
        {
            _syncingWorktrees = false;
        }
    }

    private void OnWorktreesToggleChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_worktrees is null || _syncingWorktrees)
        {
            return;
        }

        _syncingWorktrees = true;
        try
        {
            if (WorktreesToggle.IsChecked == true)
            {
                _worktrees.Open();
            }
            else
            {
                _worktrees.Close();
            }
        }
        finally
        {
            _syncingWorktrees = false;
        }
    }

    private void OnWorktreesCloseRequested(object? sender, EventArgs e) => OnCloseToolClick(sender, new Avalonia.Interactivity.RoutedEventArgs());

    #endregion

    #region Checkpoints toggle

    // The Checkpoints toggle shows or hides the timeline of the selected session.
    private void OnCheckpointsToggleChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_syncingCheckpoints || _current?.CheckpointsModel is not { } model)
        {
            return;
        }

        _syncingCheckpoints = true;
        try
        {
            if (CheckpointsToggle.IsChecked == true)
            {
                _ = model.RefreshAsync(CancellationToken.None);
                model.Open();
            }
            else
            {
                model.Close();
            }
        }
        finally
        {
            _syncingCheckpoints = false;
        }
    }

    private void SyncCheckpointsToggle()
    {
        _syncingCheckpoints = true;
        try
        {
            CheckpointsToggle.IsChecked = _current?.CheckpointsModel is { IsOpen: true };
            CheckpointsToggle.IsEnabled = _current?.CheckpointsModel is not null;
        }
        finally
        {
            _syncingCheckpoints = false;
        }
    }

    #endregion

    #region Cloud

    private void InitializeCloud()
    {
        if (_services.Cloud is not { } cloud)
        {
            return;
        }

        _cloudChanged = (_, _) => Dispatcher.UIThread.Post(() => _ = RefreshCloudRowsAsync(cloud));
        cloud.Changes += _cloudChanged;
        _ = RefreshCloudRowsAsync(cloud);
    }

    // The agents list of the control plane decides the status words of the rail rows of cloud sessions.
    private async Task RefreshCloudRowsAsync(ICloudAgentsHost cloud)
    {
        try
        {
            var agents = await cloud.ListAsync(CancellationToken.None).ConfigureAwait(true);
            if (!_closed)
            {
                _panel.Catalog.SetCloudAgents(agents);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            Trace.TraceWarning($"OmpOS could not read the cloud agents ({exception.GetType().Name}).");
        }
    }

    // /in-cloud and the & prefix: the user names the repository and confirms what the worker costs before anything is created.
    private async Task<SlashActionResult> HandOffToCloudAsync(string task, CancellationToken cancellationToken)
    {
        if (_services.Cloud is not { } cloud)
        {
            return SlashActionResult.NotAvailable(CloudCopy.NotConfiguredShort + " " + CloudCopy.NotConfiguredIntro);
        }

        if (string.IsNullOrWhiteSpace(task))
        {
            return SlashActionResult.Failed(CloudCopy.HandoffNeedsPrompt);
        }

        CloudSetup setup;
        try
        {
            setup = await cloud.GetSetupAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (CloudHostException failure)
        {
            return SlashActionResult.NotAvailable(failure.Message);
        }

        if (!setup.IsConfigured)
        {
            return SlashActionResult.NotAvailable(setup.NotConfiguredReason is { Length: > 0 } reason ? reason : CloudCopy.NotConfiguredShort);
        }

        var model = new CloudHandoffModel(new CloudHandoff(cloud), setup, task.Trim());
        var result = await CloudHandoffDialog(model, TopLevel.GetTopLevel(this) as Window).ConfigureAwait(true);
        return result switch
        {
            { Outcome: CloudHandoffOutcome.Started } started => SlashActionResult.Completed(started.Message),
            null => SlashActionResult.Completed(CloudCopy.CreateCancelled),
            _ => SlashActionResult.Failed(result.Message),
        };
    }

    private static async Task<CloudHandoffResult?> ShowCloudHandoffAsync(CloudHandoffModel model, Window? owner)
    {
        if (owner is null)
        {
            return null;
        }

        var window = new CloudHandoffWindow(model);
        await window.ShowDialog(owner).ConfigureAwait(true);
        return window.Result;
    }

    #endregion

    #region Workspace sets

    /// <summary>Gets the task of the last workspace dialog the rail button opened, so a test can wait for it. It is completed before the first click.</summary>
    internal Task WorkspaceDialogTask { get; private set; } = Task.CompletedTask;

    private void OnWorkspacesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => WorkspaceDialogTask = ShowWorkspaceDialogAsync();

    // The workspace sets dialog: the folders of the next session. Start saves the set, and starts a session on it when the head can.
    private async Task ShowWorkspaceDialogAsync()
    {
        if (_services.WorkspacePicker is not { } backend)
        {
            return;
        }

        var picker = new WorkspaceSetPicker(backend);
        picker.AddFolder(_current?.Runtime?.WorkspaceRoot ?? _workspace.FullPath);
        try
        {
            if (!await WorkspaceDialog(picker, TopLevel.GetTopLevel(this) as Window).ConfigureAwait(true))
            {
                return;
            }

            await StartOnRootsAsync(picker.Name, picker.Snapshot(), CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            SetStatus($"{FaultedStatus} {exception.GetBaseException().Message}");
        }
    }

    private static async Task<bool> ShowWorkspaceWindowAsync(WorkspaceSetPicker picker, Window? owner)
    {
        if (owner is null)
        {
            return false;
        }

        var window = new WorkspaceSetPickerWindow(picker);
        await window.ShowDialog(owner).ConfigureAwait(true);
        return window.Started;
    }

    // Saves the set under its name, then starts a new session whose folders are the set's: the engine set takes the pending roots when it creates the session.
    private async Task<bool> StartOnRootsAsync(string name, IReadOnlyList<WorkspacePickerRoot> roots, CancellationToken cancellationToken)
    {
        if (roots.Count == 0)
        {
            return false;
        }

        if (_services.WorkspaceSets is { } store)
        {
            await store.RecordAsync(WorkspaceSet.FromPaths(name, roots.Select(static root => root.Path), roots[0].EnvironmentId), cancellationToken).ConfigureAwait(true);
        }

        if (_services.NewSessionTarget is not { } pending)
        {
            throw new InvalidOperationException("This window cannot start a session on several folders.");
        }

        pending.SetRoots(roots);
        await StartNewAgentAsync().ConfigureAwait(true);
        return true;
    }

    #endregion
}

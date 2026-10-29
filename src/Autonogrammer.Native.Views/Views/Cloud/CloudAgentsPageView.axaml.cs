using System.ComponentModel;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Cloud;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Autonogrammer.Native.Views.Cloud;

/// <summary>
/// The Cloud agents page: the list of cloud agents with a status word and glyph, a status filter, the details of the selected agent with
/// Wake, Stop, Delete (after a confirmation), Open in Agents, Send and the pull request buttons, and the new-agent form in place of the
/// list while it is open. The view draws exactly one state at a time. When cloud agents are not set up it shows the setup steps and no
/// agent control. The model loads when it becomes the data context, and its relative times refresh on a timer while the view is attached.
/// </summary>
internal partial class CloudAgentsPageView : UserControl
{
    private static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(30);

    private readonly DispatcherTimer _clock;
    private CloudAgentsViewModel? _model;
    private CloudAgentDetailsModel? _details;
    private NewCloudAgentView? _newAgent;
    private NewCloudAgentModel? _newAgentModel;
    private bool _syncing;
    private bool _wasConfirmingDelete;
    private bool _attached;

    /// <summary>Creates the page with no model.</summary>
    public CloudAgentsPageView()
    {
        InitializeComponent();
        _clock = new DispatcherTimer { Interval = ClockInterval };
        _clock.Tick += OnClockTick;
        AgentList.ContainerPrepared += OnRowContainerPrepared;

        // Enter is handled while it tunnels down, before the text box can use it for itself.
        PromptBox.AddHandler(KeyDownEvent, OnPromptKeyDown, RoutingStrategies.Tunnel);
        SetupStepList.ItemsSource = CloudCopy.SetupSteps.Select(static (step, index) => CloudCopy.SetupStepLine(index + 1, step)).ToArray();
    }

    /// <summary>Gets the New cloud agent button.</summary>
    internal Button NewAgent => NewAgentButton;

    /// <summary>Gets the Refresh button.</summary>
    internal Button Refresh => RefreshButton;

    /// <summary>Gets the status filter.</summary>
    internal ComboBox StatusFilter => StatusFilterBox;

    /// <summary>Gets the list of agents.</summary>
    internal ListBox Agents => AgentList;

    /// <summary>Gets the loading text.</summary>
    internal TextBlock Loading => LoadingText;

    /// <summary>Gets the panel of the not-set-up state.</summary>
    internal StackPanel NotConfigured => NotConfiguredPanel;

    /// <summary>Gets the list of setup steps.</summary>
    internal ItemsControl SetupSteps => SetupStepList;

    /// <summary>Gets the Check again button of the not-set-up state.</summary>
    internal Button CheckAgain => CheckAgainButton;

    /// <summary>Gets the panel of the failed-load state.</summary>
    internal StackPanel LoadFailed => ErrorPanel;

    /// <summary>Gets the Try again button of the failed-load state.</summary>
    internal Button TryAgain => TryAgainButton;

    /// <summary>Gets the panel of the no-agent state.</summary>
    internal StackPanel Empty => EmptyPanel;

    /// <summary>Gets the banner of a failed refresh.</summary>
    internal Border RefreshError => RefreshErrorBanner;

    /// <summary>Gets the panel of the list and details.</summary>
    internal StackPanel Ready => ReadyPanel;

    /// <summary>Gets the host of the new-agent form.</summary>
    internal ContentControl NewAgentArea => NewAgentHost;

    /// <summary>Gets the card of the selected agent's details.</summary>
    internal Border Details => DetailsCard;

    /// <summary>Gets the Wake button.</summary>
    internal Button Wake => WakeButton;

    /// <summary>Gets the Stop button.</summary>
    internal Button Stop => StopButton;

    /// <summary>Gets the Open in Agents button.</summary>
    internal Button OpenInAgents => OpenInAgentsButton;

    /// <summary>Gets the Delete button.</summary>
    internal Button Delete => DeleteButton;

    /// <summary>Gets the bar of the delete confirmation.</summary>
    internal Border DeleteConfirmation => DeleteConfirmBar;

    /// <summary>Gets the button that confirms a delete.</summary>
    internal Button ConfirmDelete => ConfirmDeleteButton;

    /// <summary>Gets the button that cancels a delete.</summary>
    internal Button CancelDelete => CancelDeleteButton;

    /// <summary>Gets the message box.</summary>
    internal TextBox Prompt => PromptBox;

    /// <summary>Gets the Send button.</summary>
    internal Button Send => SendButton;

    /// <summary>Gets the list of pull request lines.</summary>
    internal ItemsControl PullRequests => PullRequestList;

    /// <summary>Gets the list of history steps.</summary>
    internal ItemsControl Timeline => TimelineList;

    /// <summary>Gets the polite announcement of a finished command.</summary>
    internal TextBlock Notice => NoticeText;

    /// <summary>Gets the failure message of a command.</summary>
    internal TextBlock ActionError => ActionErrorText;

    /// <summary>Gets the new-agent form while it is open, or null.</summary>
    internal NewCloudAgentView? NewAgentForm => _newAgent;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _clock.Start();
        Render();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _clock.Stop();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        CloseNewAgent(false);
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        SetDetails(null);
        var model = DataContext as CloudAgentsViewModel;
        _model = model;
        if (model is not null)
        {
            model.PropertyChanged += OnModelChanged;
            _syncing = true;
            try
            {
                StatusFilterBox.ItemsSource = model.FilterOptions;
                StatusFilterBox.SelectedItem = model.FilterOptions.FirstOrDefault(option => option.Status == model.StatusFilter);
            }
            finally
            {
                _syncing = false;
            }

            SetDetails(model.Details);

            // LoadAsync reports its expected failures in the model's state and never throws them, so the task is not observed here.
            _ = model.LoadAsync(CancellationToken.None);
        }

        Render();
    }

    // The row container is what a keyboard or a screen reader lands on, so it carries the row's accessible name.
    private void OnRowContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (AgentList.ItemsSource is IReadOnlyList<CloudAgentRow> rows && e.Index >= 0 && e.Index < rows.Count)
        {
            AutomationProperties.SetName(e.Container, rows[e.Index].AccessibleName);
        }
    }

    private void OnClockTick(object? sender, EventArgs e) => _model?.RefreshTimes();

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(CloudAgentsViewModel.Details), StringComparison.Ordinal) && _model is not null)
        {
            SetDetails(_model.Details);
        }

        Render();
    }

    private void SetDetails(CloudAgentDetailsModel? details)
    {
        if (ReferenceEquals(_details, details))
        {
            return;
        }

        if (_details is not null)
        {
            _details.PropertyChanged -= OnDetailsChanged;
        }

        _details = details;
        _wasConfirmingDelete = false;
        if (_details is not null)
        {
            _details.PropertyChanged += OnDetailsChanged;
        }
    }

    private void OnDetailsChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void Render()
    {
        if (!_attached && _model is null)
        {
            return;
        }

        var focused = FocusedButtonName();
        var state = _model?.State ?? CloudAgentsViewState.Loading;
        var formOpen = _newAgent is not null;
        var usable = state is CloudAgentsViewState.Empty or CloudAgentsViewState.Ready;

        LoadingText.IsVisible = state == CloudAgentsViewState.Loading;
        NotConfiguredPanel.IsVisible = state == CloudAgentsViewState.NotConfigured;
        ErrorPanel.IsVisible = state == CloudAgentsViewState.Error;
        EmptyPanel.IsVisible = state == CloudAgentsViewState.Empty && !formOpen;
        ReadyPanel.IsVisible = state == CloudAgentsViewState.Ready && !formOpen;
        NewAgentHost.IsVisible = formOpen;

        // The toolbar belongs to the states that have agents to act on; it is absent, not dimmed, while cloud is not set up or loading.
        Toolbar.IsVisible = usable && !formOpen;
        FilterPanel.IsVisible = state == CloudAgentsViewState.Ready;
        NewAgentButton.IsEnabled = _model?.CanCreate == true;
        RefreshButton.IsEnabled = _model?.IsRefreshing != true;
        RefreshErrorBanner.IsVisible = state == CloudAgentsViewState.Ready && !formOpen && !string.IsNullOrEmpty(_model?.RefreshError);

        RenderList();
        RenderDetails();
        RestoreFocusLater(focused);
    }

    private void RenderList()
    {
        var model = _model;
        var rows = model?.Rows ?? [];
        var current = AgentList.ItemsSource as IReadOnlyList<CloudAgentRow>;
        _syncing = true;
        try
        {
            if (current is null || !current.SequenceEqual(rows))
            {
                AgentList.ItemsSource = rows;
            }

            var selected = rows.FirstOrDefault(row => string.Equals(row.Id, model?.SelectedId, StringComparison.Ordinal));
            if (!ReferenceEquals(AgentList.SelectedItem, selected) && !Equals(AgentList.SelectedItem, selected))
            {
                AgentList.SelectedItem = selected;
            }

            var option = model?.FilterOptions.FirstOrDefault(candidate => candidate.Status == model.StatusFilter);
            if (!ReferenceEquals(StatusFilterBox.SelectedItem, option))
            {
                StatusFilterBox.SelectedItem = option;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RenderActions(CloudAgentDetailsModel details, string name)
    {
        WakeButton.IsVisible = CloudAgentStatusMap.CanWake(details.Status);
        WakeButton.IsEnabled = details.CanWake;
        AutomationProperties.SetName(WakeButton, CloudCopy.WakeName(name));
        StopButton.IsVisible = CloudAgentStatusMap.CanStop(details.Status);
        StopButton.IsEnabled = details.CanStop;
        AutomationProperties.SetName(StopButton, CloudCopy.StopName(name));
        OpenInAgentsButton.IsEnabled = details.CanOpenInAgents;
        AutomationProperties.SetName(OpenInAgentsButton, CloudCopy.OpenInAgentsName(name));
        DeleteButton.IsEnabled = details.CanDelete;
        AutomationProperties.SetName(DeleteButton, CloudCopy.DeleteName(name));

        DeleteConfirmBar.IsVisible = details.IsConfirmingDelete;
        DeleteWarningText.Text = details.DeleteWarning;
        AutomationProperties.SetName(ConfirmDeleteButton, CloudCopy.ConfirmDeleteName(name));
        ConfirmDeleteButton.IsEnabled = !details.IsBusy;
        FollowDeleteConfirmation(details);
    }

    private void RenderDetails()
    {
        var details = _details;
        var showDetails = details is not null && _model?.State == CloudAgentsViewState.Ready;
        DetailsCard.IsVisible = showDetails;
        NoSelectionText.IsVisible = !showDetails && _model?.State == CloudAgentsViewState.Ready && _model.Rows.Count > 0;
        if (details is null)
        {
            return;
        }

        var name = details.Name;
        DetailName.Text = name;
        DetailStatusChip.Text = details.StatusText;
        DetailStatusChip.IconKey = details.IconKey;
        DetailStatusChip.Tone = CloudStatusVisuals.ToneOf(details.Status);
        DetailStatusDescription.Text = details.StatusDescription;
        DetailStatusReason.Text = details.StatusDetail;
        DetailStatusReason.IsVisible = details.StatusDetail.Length > 0;
        DetailBranch.Text = details.BranchText;
        DetailBaseBranch.Text = details.BaseBranchText;
        DetailEnvironment.Text = details.EnvironmentText;
        DetailModel.Text = details.ModelText;
        DetailIdle.Text = details.IdleText;
        DetailIdle.IsVisible = details.IdleText.Length > 0;
        DetailLastActivity.Text = details.LastActivityText;

        SetItems(RepositoryList, details.Repositories);
        SetItems(PullRequestList, details.PullRequests);
        SetItems(TimelineList, details.Timeline);
        NoHistoryText.IsVisible = details.Timeline.Count == 0;

        RenderActions(details, name);

        if (!string.Equals(PromptBox.Text ?? string.Empty, details.PromptText, StringComparison.Ordinal))
        {
            PromptBox.Text = details.PromptText;
        }

        PromptBox.IsEnabled = details.CanPrompt;
        AutomationProperties.SetName(PromptBox, CloudCopy.PromptName(name));
        SendButton.IsEnabled = details.CanSendPrompt;
        AutomationProperties.SetName(SendButton, CloudCopy.SendName(name));

        NoticeText.Text = details.Notice;
        NoticeText.IsVisible = details.Notice.Length > 0;
        ActionErrorText.Text = details.ActionError;
        ActionErrorText.IsVisible = details.ActionError.Length > 0;
    }

    // Focus goes to Cancel when the confirmation opens, and back to Delete (or the list when Delete is gone) when it closes.
    private void FollowDeleteConfirmation(CloudAgentDetailsModel details)
    {
        if (details.IsConfirmingDelete && !_wasConfirmingDelete)
        {
            CancelDeleteButton.FocusWhenReady();
        }
        else if (!details.IsConfirmingDelete && _wasConfirmingDelete)
        {
            (details.CanDelete ? DeleteButton : (Control)AgentList).FocusWhenReady();
        }

        _wasConfirmingDelete = details.IsConfirmingDelete;
    }

    // Rows are value records, so an unchanged list is not reassigned: reassigning rebuilds the buttons and takes focus with them.
    private static void SetItems<T>(ItemsControl control, IReadOnlyList<T> items)
    {
        if (control.ItemsSource is IReadOnlyList<T> current && current.SequenceEqual(items))
        {
            return;
        }

        control.ItemsSource = items;
    }

    private void OnAgentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _model is null)
        {
            return;
        }

        _model.Select((AgentList.SelectedItem as CloudAgentRow)?.Id);
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _model is null || StatusFilterBox.SelectedItem is not CloudFilterOption option)
        {
            return;
        }

        _model.StatusFilter = option.Status;
    }

    private void OnNewAgentClick(object? sender, RoutedEventArgs e)
    {
        if (_model?.BeginNewAgent() is not { } model)
        {
            return;
        }

        _newAgentModel = model;
        model.Created += OnNewAgentFinished;
        model.CancelRequested += OnNewAgentFinished;
        _newAgent = new NewCloudAgentView { DataContext = model };
        NewAgentHost.Content = _newAgent;
        Render();
        _newAgent.FocusFirstField();
    }

    private void OnNewAgentFinished(object? sender, EventArgs e) => CloseNewAgent(true);

    private void CloseNewAgent(bool restoreFocus)
    {
        if (_newAgentModel is not null)
        {
            _newAgentModel.Created -= OnNewAgentFinished;
            _newAgentModel.CancelRequested -= OnNewAgentFinished;
        }

        var wasOpen = _newAgent is not null;
        _newAgentModel = null;
        _newAgent = null;
        NewAgentHost.Content = null;
        if (wasOpen)
        {
            Render();
            if (restoreFocus)
            {
                (AgentList.SelectedItem is not null ? AgentList : (Control)NewAgentButton).FocusWhenReady();
            }
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        // RefreshAsync reports its expected failures through the model and never throws them, so the task is not observed here.
        _ = _model?.RefreshAsync(CancellationToken.None);
    }

    private void OnCheckAgainClick(object? sender, RoutedEventArgs e) => _ = _model?.LoadAsync(CancellationToken.None);

    private void OnTryAgainClick(object? sender, RoutedEventArgs e) => _ = _model?.LoadAsync(CancellationToken.None);

    private void OnWakeClick(object? sender, RoutedEventArgs e) => _ = _details?.WakeAsync(CancellationToken.None);

    private void OnStopClick(object? sender, RoutedEventArgs e) => _ = _details?.StopAsync(CancellationToken.None);

    private void OnOpenInAgentsClick(object? sender, RoutedEventArgs e) => _details?.OpenInAgents();

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => _details?.RequestDelete();

    private void OnCancelDeleteClick(object? sender, RoutedEventArgs e) => _details?.CancelDelete();

    private void OnConfirmDeleteClick(object? sender, RoutedEventArgs e) => _ = _details?.ConfirmDeleteAsync(CancellationToken.None);

    private void OnSendClick(object? sender, RoutedEventArgs e) => _ = _details?.SendPromptAsync(CancellationToken.None);

    private void OnPromptKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && _details is { CanSendPrompt: true } details)
        {
            e.Handled = true;
            _ = details.SendPromptAsync(CancellationToken.None);
        }
    }

    private void OnPromptChanged(object? sender, TextChangedEventArgs e)
    {
        if (_details is not null && !string.Equals(_details.PromptText, PromptBox.Text ?? string.Empty, StringComparison.Ordinal))
        {
            _details.PromptText = PromptBox.Text ?? string.Empty;
        }
    }

    private void OnPullRequestClick(object? sender, RoutedEventArgs e)
    {
        if (_details is not null && (sender as Control)?.DataContext is CloudPullRequestRow row)
        {
            _ = _details.OpenPullRequestAsync(row.Repository, CancellationToken.None);
        }
    }

    private string? FocusedButtonName()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Button;
        return focused is not null && IsInsideView(focused) ? AutomationProperties.GetName(focused) : null;
    }

    // The pull request rows are rebuilt when they change, which takes focus with them; the new buttons are realized after this pass, so the
    // focus is given back on a later dispatcher turn, by accessible name, and only if focus was lost in the meantime.
    private void RestoreFocusLater(string? name)
    {
        if (name is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => RestoreFocus(name), DispatcherPriority.Background);
    }

    private void RestoreFocus(string name)
    {
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Control current && IsInsideView(current))
        {
            return;
        }

        var target = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.IsVisible && button.IsEnabled && string.Equals(AutomationProperties.GetName(button), name, StringComparison.Ordinal));

        // The button that had focus is gone (Wake hides once the agent is awake): focus goes to the list, never to nowhere.
        if (target is null || !target.Focus())
        {
            _ = AgentList.Focus();
        }
    }

    private bool IsInsideView(Visual visual) =>
        ReferenceEquals(visual, this) || visual.GetVisualAncestors().Contains(this);
}

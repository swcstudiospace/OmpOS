using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.Automations;

namespace OmpOS.Native.Views.Automations;

/// <summary>
/// The Automations page: the list of user-defined automations with their triggers, target and next run, the details of the selected one with
/// Run now, Turn on or off, Edit, Delete (after a confirmation) and its run history, and the editor in place of the list while it is open.
/// The view draws exactly one state at a time: loading, unavailable (the service has automations switched off), failed load, empty, or the
/// list. The model loads when it becomes the data context; the view makes no manager call itself.
/// </summary>
internal partial class AutomationsPageView : UserControl
{
    private HostedAutomationsViewModel? _model;
    private AutomationEditorView? _editorView;
    private bool _syncing;
    private bool _wasConfirmingDelete;
    private bool _wasEditing;

    /// <summary>Creates the page with no model.</summary>
    public AutomationsPageView()
    {
        InitializeComponent();
        AutomationList.ContainerPrepared += OnRowContainerPrepared;
    }

    /// <summary>Gets the New automation button of the toolbar.</summary>
    internal Button New => NewButton;

    /// <summary>Gets the New automation button of the empty state.</summary>
    internal Button EmptyNew => EmptyNewButton;

    /// <summary>Gets the Refresh button.</summary>
    internal Button Refresh => RefreshButton;

    /// <summary>Gets the loading text.</summary>
    internal TextBlock Loading => LoadingText;

    /// <summary>Gets the panel of the unavailable state.</summary>
    internal StackPanel Unavailable => UnavailablePanel;

    /// <summary>Gets the panel of the failed-load state.</summary>
    internal StackPanel LoadFailed => ErrorPanel;

    /// <summary>Gets the Try again button of the failed-load state.</summary>
    internal Button TryAgain => TryAgainButton;

    /// <summary>Gets the panel of the no-automation state.</summary>
    internal StackPanel Empty => EmptyPanel;

    /// <summary>Gets the panel of the list and details.</summary>
    internal StackPanel Ready => ReadyPanel;

    /// <summary>Gets the host of the editor.</summary>
    internal ContentControl EditorArea => EditorHost;

    /// <summary>Gets the list of automations.</summary>
    internal ListBox AutomationItems => AutomationList;

    /// <summary>Gets the card of the selected automation.</summary>
    internal Border Details => DetailsCard;

    /// <summary>Gets the Run now button.</summary>
    internal Button RunNow => RunNowButton;

    /// <summary>Gets the Turn on or off button.</summary>
    internal Button Toggle => ToggleEnabledButton;

    /// <summary>Gets the Edit button.</summary>
    internal Button Edit => EditButton;

    /// <summary>Gets the Delete button.</summary>
    internal Button Delete => DeleteButton;

    /// <summary>Gets the bar of the delete confirmation.</summary>
    internal Border DeleteConfirmation => DeleteConfirmBar;

    /// <summary>Gets the button that confirms a delete.</summary>
    internal Button ConfirmDelete => ConfirmDeleteButton;

    /// <summary>Gets the button that cancels a delete.</summary>
    internal Button CancelDelete => CancelDeleteButton;

    /// <summary>Gets the list of history rows.</summary>
    internal ItemsControl History => HistoryList;

    /// <summary>Gets the polite announcement of a finished action.</summary>
    internal TextBlock Notice => NoticeText;

    /// <summary>Gets the failure message of an action.</summary>
    internal TextBlock ActionError => ActionErrorText;

    /// <summary>Gets the editor while it is open, or null.</summary>
    internal AutomationEditorView? EditorForm => _editorView;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.Changed -= OnModelChanged;
        }

        DetachEditor();
        var model = DataContext as HostedAutomationsViewModel;
        _model = model;
        if (model is not null)
        {
            model.Changed += OnModelChanged;

            // LoadAsync reports its expected failures in the model's state and never throws them, so the task is not observed here.
            _ = model.LoadAsync(CancellationToken.None);
        }

        Render();
    }

    // The row container is what a keyboard or a screen reader lands on, so it carries the row's accessible name.
    private void OnRowContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        if (AutomationList.ItemsSource is IReadOnlyList<HostedAutomationRowModel> rows && e.Index >= 0 && e.Index < rows.Count)
        {
            AutomationProperties.SetName(e.Container, rows[e.Index].AccessibleName);
        }
    }

    private void OnModelChanged(object? sender, EventArgs e) => Render();

    private void Render()
    {
        var model = _model;
        var state = model?.State;
        var editing = model?.Editor is not null && state is HostedAutomationsViewState.Ready or HostedAutomationsViewState.Empty;

        LoadingText.IsVisible = state == HostedAutomationsViewState.Loading;
        UnavailablePanel.IsVisible = state == HostedAutomationsViewState.Unavailable;
        ErrorPanel.IsVisible = state == HostedAutomationsViewState.Error;
        EmptyPanel.IsVisible = state == HostedAutomationsViewState.Empty && !editing;
        ReadyPanel.IsVisible = state == HostedAutomationsViewState.Ready && !editing;
        EditorHost.IsVisible = editing;
        Toolbar.IsVisible = state == HostedAutomationsViewState.Ready && !editing;
        RefreshButton.IsEnabled = model?.IsBusy != true;

        SyncEditor(editing ? model?.Editor : null);
        RenderList();
        RenderDetails();

        var notice = model?.Notice ?? string.Empty;
        NoticeText.Text = notice;
        NoticeText.IsVisible = notice.Length > 0;
        var failure = model?.ActionFailure ?? string.Empty;
        ActionErrorText.Text = failure;
        ActionErrorText.IsVisible = failure.Length > 0;
    }

    private void SyncEditor(AutomationEditorModel? editor)
    {
        if (ReferenceEquals(_editorView?.DataContext, editor) && (_editorView is not null || editor is null))
        {
            return;
        }

        DetachEditor();
        if (editor is not null)
        {
            var view = new AutomationEditorView { DataContext = editor };
            view.SaveRequested += OnEditorSaveRequested;
            view.CancelRequested += OnEditorCancelRequested;
            view.SecretStored += OnEditorSecretStored;
            _editorView = view;
            EditorHost.Content = view;
            view.FocusFirstField();
            _wasEditing = true;
        }
        else if (_wasEditing)
        {
            _wasEditing = false;
            (EmptyPanel.IsVisible ? EmptyNewButton : ReadyPanel.IsVisible ? NewButton : (Control)AutomationList).FocusWhenReady();
        }
    }

    private void DetachEditor()
    {
        if (_editorView is null)
        {
            return;
        }

        _editorView.SaveRequested -= OnEditorSaveRequested;
        _editorView.CancelRequested -= OnEditorCancelRequested;
        _editorView.SecretStored -= OnEditorSecretStored;
        _editorView = null;
        EditorHost.Content = null;
    }

    private void RenderList()
    {
        var rows = _model?.Rows ?? [];
        _syncing = true;
        try
        {
            if (!ReferenceEquals(AutomationList.ItemsSource, rows))
            {
                AutomationList.ItemsSource = rows;
            }

            var selected = _model?.SelectedRow;
            if (!ReferenceEquals(AutomationList.SelectedItem, selected))
            {
                AutomationList.SelectedItem = selected;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RenderDetails()
    {
        var model = _model;
        var row = model?.SelectedRow;
        var ready = model?.State == HostedAutomationsViewState.Ready;
        var show = row is not null && ready;
        DetailsCard.IsVisible = show;
        NoSelectionText.IsVisible = !show && ready && model?.Rows.Count > 0;
        if (model is null || row is null)
        {
            return;
        }

        DetailName.Text = row.Name;
        DetailEnabled.Text = row.EnabledText;
        DetailTrigger.Text = row.TriggerText;
        DetailTarget.Text = row.TargetText;
        DetailNextRun.Text = row.NextRunText;
        DetailLastResult.Text = row.LastResultText;
        DetailWebhook.Text = row.WebhookText;
        DetailWebhook.IsVisible = row.WebhookText.Length > 0;

        var idle = !model.IsBusy;
        RunNowButton.IsEnabled = idle;
        AutomationProperties.SetName(RunNowButton, row.RunNowName);
        ToggleEnabledButton.Content = row.ToggleText;
        ToggleEnabledButton.IsEnabled = idle;
        AutomationProperties.SetName(ToggleEnabledButton, row.ToggleName);
        EditButton.IsEnabled = idle;
        AutomationProperties.SetName(EditButton, row.EditName);
        DeleteButton.IsEnabled = idle;
        AutomationProperties.SetName(DeleteButton, row.DeleteName);

        var confirming = model.IsConfirmingDelete && string.Equals(model.PendingDeleteId, row.Id, StringComparison.Ordinal);
        DeleteConfirmBar.IsVisible = confirming;
        DeleteWarningText.Text = model.PendingDeleteText;
        AutomationProperties.SetName(ConfirmDeleteButton, AutomationsCopy.ConfirmDeleteName(row.Name));
        ConfirmDeleteButton.IsEnabled = idle;
        FollowDeleteConfirmation(confirming);

        var history = model.History;
        if (!ReferenceEquals(HistoryList.ItemsSource, history))
        {
            HistoryList.ItemsSource = history;
        }

        NoHistoryText.IsVisible = history.Count == 0 && idle;
    }

    // Focus goes to Cancel when the confirmation opens, and back to Delete when it closes.
    private void FollowDeleteConfirmation(bool confirming)
    {
        if (confirming && !_wasConfirmingDelete)
        {
            CancelDeleteButton.FocusWhenReady();
        }
        else if (!confirming && _wasConfirmingDelete)
        {
            DeleteButton.FocusWhenReady();
        }

        _wasConfirmingDelete = confirming;
    }

    private void OnAutomationSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && _model is not null && AutomationList.SelectedItem is HostedAutomationRowModel row)
        {
            // SelectAsync reports its expected failures in the model and never throws them, so the task is not observed here.
            _ = _model.SelectAsync(row.Id, CancellationToken.None);
        }
    }

    private void OnNewClick(object? sender, RoutedEventArgs e) => _model?.BeginCreate();

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        // LoadAsync reports its expected failures in the model's state and never throws them, so the task is not observed here.
        _ = _model?.LoadAsync(CancellationToken.None);
    }

    private void OnRunNowClick(object? sender, RoutedEventArgs e)
    {
        if (_model?.SelectedRow is { } row)
        {
            _ = _model.RunNowAsync(row.Id, CancellationToken.None);
        }
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e)
    {
        if (_model?.SelectedRow is { } row)
        {
            _ = _model.ToggleAsync(row.Id, CancellationToken.None);
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (_model?.SelectedRow is { } row)
        {
            _model.BeginEdit(row.Id);
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_model?.SelectedRow is { } row)
        {
            _model.RequestDelete(row.Id);
        }
    }

    private void OnCancelDeleteClick(object? sender, RoutedEventArgs e) => _model?.CancelDelete();

    private void OnConfirmDeleteClick(object? sender, RoutedEventArgs e)
    {
        _ = _model?.ConfirmDeleteAsync(CancellationToken.None);
    }

    private void OnEditorSaveRequested(object? sender, EventArgs e)
    {
        // SaveEditorAsync reports its expected failures in the editor and never throws them, so the task is not observed here.
        _ = _model?.SaveEditorAsync(CancellationToken.None);
    }

    private void OnEditorCancelRequested(object? sender, EventArgs e) => _model?.CloseEditor();

    private void OnEditorSecretStored(object? sender, EventArgs e) => _model?.AcknowledgeEditorSecret();
}

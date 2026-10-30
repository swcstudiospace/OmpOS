using System.Collections.Specialized;
using System.ComponentModel;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Worktrees;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Autonogrammer.Native.Views.Worktrees;

/// <summary>
/// The worktree runs panel: the run worktrees of the repository with Review changes, Apply, Merge three ways and Delete (after a confirmation), the patch of the reviewed run, how the last apply ended,
/// and the best-of-n section with its candidates and Keep this one. It draws the bound <see cref="WorktreeCommandsModel"/> and forwards every click to it. The model loads the runs when it becomes the
/// data context. The head shows the panel while <see cref="WorktreeCommandsModel.IsOpen"/> is true and handles <see cref="CloseRequested"/>; the status line is the live region.
/// </summary>
internal partial class WorktreeCommandsView : UserControl
{
    private const int DefaultCandidateCount = 3;

    private WorktreeCommandsModel? _model;
    private INotifyCollectionChanged? _candidates;
    private string? _localText;
    private bool _localIsError;
    private bool _wasConfirmingDelete;
    private bool _rowStatePending;

    /// <summary>Creates the panel with no model.</summary>
    public WorktreeCommandsView()
    {
        InitializeComponent();
        BestOfCountBox.ItemsSource = Enumerable.Range(1, WorktreeCommandsCopy.MaxBestOf).ToArray();
        BestOfCountBox.SelectedIndex = DefaultCandidateCount - 1;

        // Enter is handled while it tunnels down, before a text box can use it for itself.
        NewNameBox.AddHandler(KeyDownEvent, OnTextBoxKeyDown, RoutingStrategies.Tunnel);
        BestOfPromptBox.AddHandler(KeyDownEvent, OnTextBoxKeyDown, RoutingStrategies.Tunnel);
        Render();
    }

    /// <summary>Raised when the user asks to close the panel. The model has been closed by then.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets the Refresh button.</summary>
    internal Button Refresh => RefreshButton;

    /// <summary>Gets the Close button.</summary>
    internal Button Close => CloseButton;

    /// <summary>Gets the box for the name of a new worktree.</summary>
    internal TextBox NewName => NewNameBox;

    /// <summary>Gets the New worktree button.</summary>
    internal Button NewWorktree => NewWorktreeButton;

    /// <summary>Gets the status line, which is the live region of the panel.</summary>
    internal TextBlock Status => StatusText;

    /// <summary>Gets the sentence shown while there is no run worktree.</summary>
    internal TextBlock Empty => EmptyText;

    /// <summary>Gets the list of run rows.</summary>
    internal ItemsControl Runs => RunList;

    /// <summary>Gets the card of the reviewed run's patch.</summary>
    internal Border PreviewPanel => PreviewCard;

    /// <summary>Gets the sentence that sums the reviewed patch up.</summary>
    internal TextBlock PreviewSummary => PreviewSummaryText;

    /// <summary>Gets the list of files of the reviewed patch.</summary>
    internal ItemsControl PreviewFiles => PreviewFileList;

    /// <summary>Gets the card of the last apply's result.</summary>
    internal Border ApplyOutcome => ApplyOutcomeCard;

    /// <summary>Gets the sentence that says how the last apply ended.</summary>
    internal TextBlock ApplyOutcomeLine => ApplyOutcomeText;

    /// <summary>Gets the list of files that conflict.</summary>
    internal ItemsControl Conflicts => ConflictList;

    /// <summary>Gets the bar of the delete confirmation.</summary>
    internal Border DeleteConfirmation => DeleteConfirmBar;

    /// <summary>Gets the question of the delete confirmation.</summary>
    internal TextBlock DeleteWarning => DeleteWarningText;

    /// <summary>Gets the button that confirms a delete.</summary>
    internal Button ConfirmDelete => ConfirmDeleteButton;

    /// <summary>Gets the button that cancels a delete.</summary>
    internal Button CancelDelete => CancelDeleteButton;

    /// <summary>Gets the box for the task of a best-of-n run.</summary>
    internal TextBox BestOfPrompt => BestOfPromptBox;

    /// <summary>Gets the choice of the number of candidates.</summary>
    internal ComboBox BestOfCount => BestOfCountBox;

    /// <summary>Gets the button that runs the candidates.</summary>
    internal Button BestOfRun => BestOfRunButton;

    /// <summary>Gets the list of best-of-n candidates.</summary>
    internal ItemsControl Candidates => CandidateList;

    /// <summary>Binds the panel to the model in the data context, draws its current state and reads the runs.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelChanged;
        }

        if (_candidates is not null)
        {
            _candidates.CollectionChanged -= OnCandidatesChanged;
        }

        _localText = null;
        _model = DataContext as WorktreeCommandsModel;
        _candidates = _model?.Candidates;
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelChanged;
        }

        if (_candidates is not null)
        {
            _candidates.CollectionChanged += OnCandidatesChanged;
        }

        RunList.ItemsSource = _model?.Rows;
        CandidateList.ItemsSource = _model?.Candidates;
        Render();
        if (_model is { } model)
        {
            _ = RunAsync(model.RefreshAsync);
        }
    }

    // The model reports its expected failures in its status line and never throws them. A fault that still escapes (a bug, a disposed backend) is shown here, on the status line, instead of
    // being lost on a task nobody observes or thrown on the UI thread.
    private async Task RunAsync(Func<CancellationToken, Task> work)
    {
        _localText = null;
        try
        {
            await work(CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            _localText = WorktreeCommandsCopy.Failed(exception.Message);
            _localIsError = true;
            Render();
        }
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e) =>
        OnUiThread(() =>
        {
            // A sentence of the model's own replaces one the view wrote itself.
            if (string.Equals(e.PropertyName, nameof(WorktreeCommandsModel.StatusText), StringComparison.Ordinal))
            {
                _localText = null;
            }

            Render();
        });

    private void OnCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnUiThread(Render);

    private static void OnUiThread(Action action)
    {
        // The model may change off the UI thread; the controls are drawn only on the thread that owns them.
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    private void Render()
    {
        var model = _model;
        var busy = model?.IsBusy == true;
        var hasModel = model is not null;

        RefreshButton.IsEnabled = hasModel && !busy;
        NewNameBox.IsEnabled = hasModel;
        NewWorktreeButton.IsEnabled = hasModel && !busy;
        BestOfPromptBox.IsEnabled = hasModel;
        BestOfCountBox.IsEnabled = hasModel;
        BestOfRunButton.IsEnabled = hasModel && !busy;

        var empty = model?.EmptyText ?? string.Empty;
        EmptyText.Text = empty;
        EmptyText.IsVisible = empty.Length > 0;

        RenderStatus(model);
        RenderPreview(model);
        RenderOutcome(model);
        RenderDeleteConfirmation(model);
        CandidateList.IsVisible = model is { Candidates.Count: > 0 };
        ApplyRowState();
        if (!_rowStatePending)
        {
            // Rows are realized after this pass, so their buttons are brought up to date once more a moment later.
            _rowStatePending = true;
            Dispatcher.UIThread.Post(ApplyRowStateLater, DispatcherPriority.Background);
        }
    }

    private void RenderStatus(WorktreeCommandsModel? model)
    {
        var text = _localText ?? model?.StatusText ?? string.Empty;
        var isError = _localText is not null ? _localIsError : model?.HasError == true;
        StatusText.Text = text;
        StatusText.IsVisible = text.Length > 0;
        StatusText.Classes.Set("tone-error", isError);
        AutomationProperties.SetLiveSetting(StatusText, isError ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
    }

    private void RenderPreview(WorktreeCommandsModel? model)
    {
        var preview = model?.Preview;
        PreviewCard.IsVisible = preview is not null;
        if (preview is null)
        {
            return;
        }

        PreviewSummaryText.Text = WorktreeCommandsCopy.PatchSummary(preview);
        if (!ReferenceEquals(PreviewFileList.ItemsSource, preview.Files))
        {
            PreviewFileList.ItemsSource = preview.Files;
        }
    }

    private void RenderOutcome(WorktreeCommandsModel? model)
    {
        var outcome = model?.LastApply;
        ApplyOutcomeCard.IsVisible = outcome is not null;
        ApplyOutcomeText.Text = outcome?.Message ?? string.Empty;
        ApplyOutcomeText.Classes.Set("tone-error", outcome?.State is WorktreeApplyState.Conflicts or WorktreeApplyState.Failed);
        IReadOnlyList<string> conflicts = outcome?.ConflictPaths ?? [];
        if (!ReferenceEquals(ConflictList.ItemsSource, conflicts))
        {
            ConflictList.ItemsSource = conflicts;
        }

        ConflictList.IsVisible = conflicts.Count > 0;
    }

    // Focus goes to Cancel when the confirmation opens, and back to Refresh when it closes while the focus is still in this panel.
    private void RenderDeleteConfirmation(WorktreeCommandsModel? model)
    {
        var confirming = model?.IsConfirmingDelete == true;
        DeleteConfirmBar.IsVisible = confirming;
        DeleteWarningText.Text = model?.DeleteConfirmationText ?? string.Empty;
        ConfirmDeleteButton.IsEnabled = model is { IsBusy: false };
        if (confirming && !_wasConfirmingDelete)
        {
            CancelDeleteButton.FocusWhenReady();
        }
        else if (!confirming && _wasConfirmingDelete && FocusIsInPanelOrLost())
        {
            RefreshButton.FocusWhenReady();
        }

        _wasConfirmingDelete = confirming;
    }

    private void ApplyRowStateLater()
    {
        _rowStatePending = false;
        ApplyRowState();
    }

    // A row button is dimmed while a command runs, and a candidate that has no worktree cannot be kept.
    private void ApplyRowState()
    {
        var busy = _model?.IsBusy == true;
        foreach (var button in RunList.GetVisualDescendants().OfType<Button>())
        {
            button.IsEnabled = !busy;
        }

        foreach (var button in CandidateList.GetVisualDescendants().OfType<Button>())
        {
            button.IsEnabled = !busy && button.DataContext is BestOfNCandidate { Run: not null };
        }
    }

    private bool FocusIsInPanelOrLost()
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        return focused is null || ReferenceEquals(focused, this) || focused.GetVisualAncestors().Contains(this);
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (_model is { IsBusy: false } model)
        {
            _ = RunAsync(model.RefreshAsync);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        _model?.Close();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnNewWorktreeClick(object? sender, RoutedEventArgs e) => StartWorktree();

    private void StartWorktree()
    {
        if (_model is not { IsBusy: false } model)
        {
            return;
        }

        _ = RunAsync(
            async token =>
            {
                var started = await model.StartAsync(NewNameBox.Text, token).ConfigureAwait(true);
                if (started is not null)
                {
                    NewNameBox.Text = string.Empty;
                }
            });
    }

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        if (_model is { IsBusy: false } model && RowOf(sender) is { } row)
        {
            _ = RunAsync(token => model.PreviewAsync(row.RunId, token));
        }
    }

    private void OnApplyClick(object? sender, RoutedEventArgs e)
    {
        if (_model is { IsBusy: false } model && RowOf(sender) is { } row)
        {
            _ = RunAsync(token => model.ApplyAsync(row.RunId, WorktreeApplyMethod.Clean, token));
        }
    }

    private void OnApplyThreeWayClick(object? sender, RoutedEventArgs e)
    {
        if (_model is { IsBusy: false } model && RowOf(sender) is { } row)
        {
            _ = RunAsync(token => model.ApplyAsync(row.RunId, WorktreeApplyMethod.ThreeWay, token));
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_model is { IsBusy: false } model && RowOf(sender) is { } row)
        {
            _ = RunAsync(token => model.RequestDeleteAsync(row.RunId, token));
        }
    }

    private void OnConfirmDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_model is { IsBusy: false, IsConfirmingDelete: true } model)
        {
            _ = RunAsync(model.ConfirmDeleteAsync);
        }
    }

    private void OnCancelDeleteClick(object? sender, RoutedEventArgs e) => _model?.CancelDelete();

    private void OnBestOfRunClick(object? sender, RoutedEventArgs e) => RunBestOf();

    private void RunBestOf()
    {
        if (_model is not { IsBusy: false } model)
        {
            return;
        }

        var count = BestOfCountBox.SelectedItem is int chosen ? chosen : DefaultCandidateCount;
        var prompt = BestOfPromptBox.Text ?? string.Empty;
        _ = RunAsync(token => model.BestOfNAsync(prompt, count, token));
    }

    private void OnKeepClick(object? sender, RoutedEventArgs e)
    {
        if (_model is not { IsBusy: false } model || (sender as Control)?.DataContext is not BestOfNCandidate candidate)
        {
            return;
        }

        _localText = null;
        if (!model.RequestDiscardOthers(candidate.Index))
        {
            _localText = "There is no other candidate to discard.";
            _localIsError = false;
            Render();
        }
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        if (ReferenceEquals(sender, NewNameBox))
        {
            e.Handled = true;
            StartWorktree();
        }
        else if (ReferenceEquals(sender, BestOfPromptBox))
        {
            e.Handled = true;
            RunBestOf();
        }
    }

    // Escape dismisses a pending deletion from anywhere in the panel.
    private void OnViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape && _model is { IsConfirmingDelete: true } model)
        {
            e.Handled = true;
            model.CancelDelete();
        }
    }

    private static WorktreeRunRow? RowOf(object? sender) => (sender as Control)?.DataContext as WorktreeRunRow;
}

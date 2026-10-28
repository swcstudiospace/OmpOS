using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.Checkpoints;

namespace OmpOS.Native.Views.Checkpoints;

/// <summary>
/// The checkpoint timeline of one session. It draws the bound <see cref="CheckpointsModel"/>: a row per turn with Preview and Rewind buttons, Refresh and Undo last turn,
/// the confirmation block, the empty sentence and a status line that is a polite live region (assertive while it reports a failure). The panel hides itself while the model
/// is not open. A rewind is never started by a row alone: the Rewind button, like Undo last turn, only asks, and the confirmation block must be confirmed. Focus moves to
/// Cancel when the confirmation opens and returns to the row's Rewind button when it closes. Esc dismisses a pending confirmation, else closes the panel; Close does the same
/// as that second Esc: it closes the model and raises <see cref="CloseRequested"/>.
/// </summary>
internal partial class CheckpointsTimelineView : UserControl
{
    private readonly ModelWatcher<CheckpointsModel> _watch;
    private bool _attached;
    private bool _wasConfirming;
    private bool _previewRequested;
    private int? _returnTurn;

    /// <summary>Creates the panel with no model.</summary>
    public CheckpointsTimelineView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<CheckpointsModel>(OnModelChanged);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>Raised when the user closes the panel with the Close button or Esc. The model is already closed.</summary>
    internal event EventHandler? CloseRequested;

    /// <summary>Gets the Refresh button.</summary>
    internal Button Refresh => RefreshButton;

    /// <summary>Gets the Undo last turn button.</summary>
    internal Button Undo => UndoButton;

    /// <summary>Gets the Close button.</summary>
    internal Button Close => CloseButton;

    /// <summary>Gets the list of turns.</summary>
    internal ItemsControl Rows => RowList;

    /// <summary>Gets the line that shows the number of checkpoints.</summary>
    internal TextBlock Count => CountLine;

    /// <summary>Gets the sentence shown while there is no checkpoint.</summary>
    internal TextBlock Empty => EmptyLine;

    /// <summary>Gets the block that shows the confirmation and the preview of a rewind.</summary>
    internal Border Details => DetailsCard;

    /// <summary>Gets the question the confirmation asks.</summary>
    internal TextBlock Confirmation => ConfirmationLine;

    /// <summary>Gets the list of the files a rewind would change.</summary>
    internal ItemsControl PreviewFiles => PreviewList;

    /// <summary>Gets the button that confirms the rewind.</summary>
    internal Button Confirm => ConfirmButton;

    /// <summary>Gets the button that confirms the rewind and overwrites files that changed since the agent's edit.</summary>
    internal Button Force => ForceButton;

    /// <summary>Gets the button that dismisses the confirmation.</summary>
    internal Button Cancel => CancelButton;

    /// <summary>Gets the status line, a live region.</summary>
    internal TextBlock Status => StatusLine;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _attached = true;
        _watch.Set(DataContext as CheckpointsModel);
        OnModelChanged();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _attached = false;
        _watch.Set(null);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        _wasConfirming = false;
        _previewRequested = false;
        _returnTurn = null;
        if (_attached)
        {
            _watch.Set(DataContext as CheckpointsModel);
            OnModelChanged();
        }
    }

    private static void FocusSoon(Func<Control?> target) =>
        Dispatcher.UIThread.Post(() => _ = target()?.Focus(), DispatcherPriority.Input);

    private void OnModelChanged()
    {
        if (_watch.Model is not { } model)
        {
            return;
        }

        AutomationProperties.SetLiveSetting(StatusLine, model.HasError ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);

        var confirming = model.IsConfirming;
        if (confirming && !_wasConfirming)
        {
            _previewRequested = false;
            _returnTurn = model.PendingTurnId;
            FocusSoon(() => CancelButton);
        }
        else if (confirming)
        {
            _returnTurn = model.PendingTurnId ?? _returnTurn;
        }
        else if (_wasConfirming && model.IsOpen)
        {
            var turn = _returnTurn;
            FocusSoon(() => FocusTargetAfterConfirmation(turn));
        }

        _wasConfirming = confirming;
        UpdateDetails(model);
        SyncRowButtons(model);
    }

    private void UpdateDetails(CheckpointsModel model) =>
        DetailsCard.IsVisible = model.IsConfirming || (_previewRequested && model.Preview is not null);

    // The row's Rewind button when it still exists and can take focus; otherwise Refresh, otherwise Close, which is always enabled. Rows are rebuilt after a rewind,
    // so the button is looked up when the focus is given, never held.
    private Control? FocusTargetAfterConfirmation(int? turn)
    {
        if (turn is { } id)
        {
            var rewind = RowList.GetVisualDescendants()
                .OfType<Button>()
                .FirstOrDefault(button => button.Classes.Contains("row-rewind") && button.IsEffectivelyEnabled && button.DataContext is CheckpointRow row && row.TurnId == id);
            if (rewind is not null)
            {
                return rewind;
            }
        }

        return RefreshButton.IsEffectivelyEnabled ? RefreshButton : CloseButton;
    }

    // A row button is enabled while its command may run, so a busy model never leaves a button that does nothing.
    private void SyncRowButtons(CheckpointsModel model)
    {
        foreach (var button in RowList.GetVisualDescendants().OfType<Button>())
        {
            SyncRowButton(button, model);
        }
    }

    private static void SyncRowButton(Button button, CheckpointsModel model)
    {
        if (button.DataContext is not CheckpointRow row)
        {
            return;
        }

        if (button.Classes.Contains("row-rewind"))
        {
            button.IsEnabled = model.RewindCommand.CanExecute(row.TurnId);
        }
        else if (button.Classes.Contains("row-preview"))
        {
            button.IsEnabled = model.PreviewCommand.CanExecute(row.TurnId);
        }
    }

    private void RequestClose()
    {
        if (_watch.Model is { } model)
        {
            _previewRequested = false;
            model.Close();
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None || _watch.Model is not { } model)
        {
            return;
        }

        e.Handled = true;
        if (model.IsConfirming)
        {
            model.CancelCommand.Execute(null);
        }
        else
        {
            RequestClose();
        }
    }

    private void OnRowButtonAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Button button && _watch.Model is { } model)
        {
            SyncRowButton(button, model);
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e) => ClearPreview();

    private void OnUndoClick(object? sender, RoutedEventArgs e) => ClearPreview();

    private void OnCloseClick(object? sender, RoutedEventArgs e) => RequestClose();

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        if (_watch.Model is { } model && (sender as Control)?.DataContext is CheckpointRow row)
        {
            _previewRequested = true;
            model.PreviewCommand.Execute(row.TurnId);
            UpdateDetails(model);
        }
    }

    private void OnRewindClick(object? sender, RoutedEventArgs e)
    {
        if (_watch.Model is { } model && (sender as Control)?.DataContext is CheckpointRow row)
        {
            _previewRequested = false;
            model.RewindCommand.Execute(row.TurnId);
            UpdateDetails(model);
        }
    }

    private void ClearPreview()
    {
        _previewRequested = false;
        if (_watch.Model is { } model)
        {
            UpdateDetails(model);
        }
    }
}

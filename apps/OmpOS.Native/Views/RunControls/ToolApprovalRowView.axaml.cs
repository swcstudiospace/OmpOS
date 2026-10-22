using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmpOS.Presentation.Approvals;

namespace OmpOS.Native.Views.RunControls;

/// <summary>
/// One pending tool-approval request: the tool, its arguments summary, the status the coordinator recorded, and the Approve and
/// Deny controls. A click is the only way a decision is sent, so the request is never allowed by default. Both controls are
/// disabled while a decision is in flight and once the request no longer waits for a decision.
/// </summary>
internal partial class ToolApprovalRowView : UserControl
{
    private readonly PendingToolApproval _approval;
    private readonly ToolApprovalCoordinator _coordinator;
    private readonly string? _unavailableDetail;
    private int _inFlight;
    private string? _faultMessage;

    /// <summary>Creates the row for one request.</summary>
    /// <param name="approval">The pending request the row shows.</param>
    /// <param name="model">The run controls whose coordinator sends the decisions.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public ToolApprovalRowView(PendingToolApproval approval, RunControlsViewModel model)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();
        _approval = approval;
        _coordinator = model.Coordinator;
        _unavailableDetail = model.UnavailableDetail;

        ToolNameText.Text = $"Tool: {approval.ToolName}";
        ArgumentsText.Text = $"Arguments: {approval.ArgumentsSummary}";
        ArgumentsText.IsVisible = approval.ArgumentsSummary.Length > 0;

        ApproveButton.Click += OnApproveClick;
        DenyButton.Click += OnDenyClick;
        Refresh();
    }

    /// <summary>Follows the request's status while the row is shown.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _approval.PropertyChanged += OnApprovalChanged;
        Refresh();
    }

    /// <summary>Stops following the request once the row leaves the tree, so the row is not kept alive by it.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _approval.PropertyChanged -= OnApprovalChanged;
    }

    private void OnApprovalChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Refresh();

    private void OnApproveClick(object? sender, RoutedEventArgs e) => Decide(ToolApprovalDecision.Allow);

    private void OnDenyClick(object? sender, RoutedEventArgs e) => Decide(ToolApprovalDecision.Deny);

    private void Decide(ToolApprovalDecision decision)
    {
        _faultMessage = null;
        _inFlight++;
        Refresh();

        var work = _coordinator.DecideAsync(_approval, decision, CancellationToken.None);

        // The coordinator completes on the UI thread. Faults and settlement are marshalled back here, so no faulted task goes unobserved.
        _ = work.ContinueWith(
            finished => Dispatcher.UIThread.Post(() => Settle(finished.Exception)),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void Settle(AggregateException? fault)
    {
        _inFlight--;
        _faultMessage = fault is null ? null : $"{MainWindow.FaultedStatus} {fault.GetBaseException().Message}";
        Refresh();
    }

    private void Refresh()
    {
        var waiting = _approval.Status is ToolApprovalStatus.Pending or ToolApprovalStatus.Refused or ToolApprovalStatus.Failed;
        var canDecide = _inFlight == 0 && waiting;
        ApproveButton.IsEnabled = canDecide;
        DenyButton.IsEnabled = canDecide;

        var message = CurrentMessage();
        StatusText.Text = message;
        StatusText.IsVisible = message.Length > 0;
        StatusText.Classes.Set("error", _faultMessage is not null || _approval.Status is ToolApprovalStatus.Failed or ToolApprovalStatus.NotFound);
    }

    private string CurrentMessage()
    {
        if (_faultMessage is not null)
        {
            return _faultMessage;
        }

        // A failed decision that the bridge could not record names the reason the bridge reported, so the user sees why.
        return _approval.Status == ToolApprovalStatus.Failed && _unavailableDetail is not null
            ? $"{_approval.StatusMessage} {_unavailableDetail}"
            : _approval.StatusMessage;
    }
}

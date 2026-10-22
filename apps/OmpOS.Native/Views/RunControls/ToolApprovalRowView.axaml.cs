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
/// <remarks>
/// Work that the row posts to the UI thread belongs to the row's attachment. The attachment owns a cancellation source that is
/// cancelled when the row leaves the visual tree, and each posted action re-checks it, so a decision that completes after the
/// row has gone never touches the row's controls.
/// </remarks>
internal partial class ToolApprovalRowView : UserControl
{
    private readonly PendingToolApproval _approval;
    private readonly ToolApprovalCoordinator _coordinator;
    private readonly string? _unavailableDetail;
    private CancellationTokenSource? _attachment;
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

    /// <summary>Follows the request's status while the row is shown, and opens the attachment that scopes its posted work.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        var attachment = new CancellationTokenSource();
        _attachment = attachment;
        _approval.PropertyChanged += OnApprovalChanged;
        Refresh();
    }

    /// <summary>
    /// Stops following the request once the row leaves the tree, so the row is not kept alive by it, and cancels the posted work
    /// the attachment scoped, so no leftover job runs after the row has gone.
    /// </summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _approval.PropertyChanged -= OnApprovalChanged;

        if (_attachment is not null)
        {
            _attachment.Cancel();
            _attachment.Dispose();
            _attachment = null;
        }
    }

    private void OnApprovalChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshOnUIThread();

    private void OnApproveClick(object? sender, RoutedEventArgs e) => Decide(ToolApprovalDecision.Allow);

    private void OnDenyClick(object? sender, RoutedEventArgs e) => Decide(ToolApprovalDecision.Deny);

    private void Decide(ToolApprovalDecision decision)
    {
        _faultMessage = null;
        _inFlight++;
        Refresh();

        var attachment = CurrentAttachment();
        var work = _coordinator.DecideAsync(_approval, decision, CancellationToken.None);

        // The coordinator completes on the UI thread. Faults and settlement are posted back, scoped to this attachment.
        _ = work.ContinueWith(
            finished => PostWhileAttached(() => Settle(finished.Exception), attachment),
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

    /// <summary>Refreshes the row on the UI thread. The status changes on whichever thread completes the decision, so a change is marshalled here.</summary>
    private void RefreshOnUIThread()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Refresh();
        }
        else
        {
            PostWhileAttached(Refresh, CurrentAttachment());
        }
    }

    /// <summary>
    /// Posts <paramref name="action"/> to the UI thread, scoped to <paramref name="attachment"/>. The job is cancelled with the
    /// attachment, and the action runs only while the attachment is still open and the row is still in a visual tree.
    /// </summary>
    private void PostWhileAttached(Action action, CancellationToken attachment)
    {
        if (attachment.IsCancellationRequested)
        {
            return;
        }

        _ = Dispatcher.UIThread.InvokeAsync(
            () =>
            {
                if (attachment.IsCancellationRequested || VisualRoot is null)
                {
                    return;
                }

                action();
            },
            DispatcherPriority.Default,
            attachment);
    }

    /// <summary>The token of the current attachment, or a cancelled token when the row is not attached.</summary>
    private CancellationToken CurrentAttachment() => _attachment?.Token ?? new CancellationToken(canceled: true);

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

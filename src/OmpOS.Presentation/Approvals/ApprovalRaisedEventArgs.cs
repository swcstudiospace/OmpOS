namespace OmpOS.Presentation.Approvals;

/// <summary>The approval request a <see cref="TerminalApprovalAdapter"/> raised for a proposed command.</summary>
public sealed class ApprovalRaisedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ApprovalRaisedEventArgs"/> class.</summary>
    /// <param name="approval">The pending approval request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="approval"/> is null.</exception>
    public ApprovalRaisedEventArgs(PendingToolApproval approval)
    {
        ArgumentNullException.ThrowIfNull(approval);
        Approval = approval;
    }

    /// <summary>Gets the pending approval request.</summary>
    public PendingToolApproval Approval { get; }
}

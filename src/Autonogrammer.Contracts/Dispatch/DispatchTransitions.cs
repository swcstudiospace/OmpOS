namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Compare-and-set table for hub command states. An edge that is not listed is a no-op rejection, never an error to the peer.
/// The hub must use <see cref="HubMayApply"/>: it never marks <see cref="DispatchCommandState.Completed"/> itself.
/// <see cref="DispatchCommandState.Received"/> to <see cref="DispatchCommandState.AwaitingApproval"/> is the device
/// <c>approval_pending</c> edge; without it that state cannot be reached.
/// Delivered to queued is the delivery loop's compensation for a frame write that failed after delivery was recorded. The hub never requests it.
/// </summary>
public static class DispatchTransitions
{
    /// <summary>Reports whether a compare-and-set from <paramref name="from"/> to <paramref name="to"/> is in the table.</summary>
    /// <param name="from">The state the row must currently have.</param>
    /// <param name="to">The state the caller wants.</param>
    /// <returns><see langword="true"/> when the edge is allowed.</returns>
    public static bool Allows(DispatchCommandState from, DispatchCommandState to) => (from, to) switch
    {
        (DispatchCommandState.Queued, DispatchCommandState.Delivered) => true,
        (DispatchCommandState.Queued, DispatchCommandState.Expired) => true,
        (DispatchCommandState.Queued, DispatchCommandState.Cancelled) => true,
        (DispatchCommandState.Delivered, DispatchCommandState.Received) => true,
        (DispatchCommandState.Delivered, DispatchCommandState.Expired) => true,
        (DispatchCommandState.Delivered, DispatchCommandState.Cancelled) => true,
        (DispatchCommandState.Delivered, DispatchCommandState.Queued) => true,
        (DispatchCommandState.Received, DispatchCommandState.AwaitingApproval) => true,
        (DispatchCommandState.Received, DispatchCommandState.Accepted) => true,
        (DispatchCommandState.Received, DispatchCommandState.Failed) => true,
        (DispatchCommandState.Received, DispatchCommandState.Expired) => true,
        (DispatchCommandState.AwaitingApproval, DispatchCommandState.Accepted) => true,
        (DispatchCommandState.AwaitingApproval, DispatchCommandState.Failed) => true,
        (DispatchCommandState.Accepted, DispatchCommandState.Running) => true,
        (DispatchCommandState.Accepted, DispatchCommandState.Failed) => true,
        (DispatchCommandState.Running, DispatchCommandState.Completed) => true,
        (DispatchCommandState.Running, DispatchCommandState.Failed) => true,
        (DispatchCommandState.Running, DispatchCommandState.Cancelled) => true,
        _ => false,
    };

    /// <summary>
    /// Reports whether the hub itself may apply the edge. Completion is device-reported and queuing belongs to the delivery loop, so both are false here.
    /// </summary>
    /// <param name="from">The state the row must currently have.</param>
    /// <param name="to">The state the hub wants to write.</param>
    /// <returns><see langword="true"/> when the hub may apply the edge.</returns>
    public static bool HubMayApply(DispatchCommandState from, DispatchCommandState to) =>
        to is not DispatchCommandState.Completed and not DispatchCommandState.Queued && Allows(from, to);
}

namespace OmpOS.Presentation.Approvals;

/// <summary>
/// Sends the user's decision on a pending request and records what the server reported (ADE-02). Each outcome maps to one
/// status and one user-visible message. Only an accepted allow reaches <see cref="ToolApprovalStatus.Allowed"/>, and the
/// coordinator makes no decision of its own, so a request is never allowed by default.
/// </summary>
/// <remarks>
/// A request that is already decided, or whose decision is in flight, is not sent again. A refused, failed or pending request
/// may be decided again: a refused request stays pending on the server, so the user may still deny it. The coordinator is not
/// thread-safe; it runs on the UI thread that owns the presentation models.
/// </remarks>
public sealed class ToolApprovalCoordinator
{
    private readonly IToolApprovalBridge _bridge;
    private readonly HashSet<PendingToolApproval> _inFlight = new(ReferenceEqualityComparer.Instance);

    /// <summary>Creates a coordinator that sends decisions through <paramref name="bridge"/>.</summary>
    /// <param name="bridge">The permission bridge that records each decision.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bridge"/> is null.</exception>
    public ToolApprovalCoordinator(IToolApprovalBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(bridge);

        _bridge = bridge;
    }

    /// <summary>
    /// Sends the decision for one request and records the outcome on it. Nothing is sent when the request is already decided
    /// or a decision for it is in flight; the request then keeps its current status.
    /// </summary>
    /// <param name="approval">The pending request.</param>
    /// <param name="decision">The user's decision.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the outcome is recorded, or at once when the request cannot be decided now.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="approval"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="decision"/> is not a defined value.</exception>
    public async Task DecideAsync(PendingToolApproval approval, ToolApprovalDecision decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        if (!CanDecide(approval) || !_inFlight.Add(approval))
        {
            return;
        }

        try
        {
            var outcome = await _bridge.DecideAsync(approval.SessionId, approval.RequestId, decision, cancellationToken).ConfigureAwait(true);
            approval.Record(StatusFor(outcome, decision), MessageFor(outcome, decision));
        }
        finally
        {
            _inFlight.Remove(approval);
        }
    }

    private static bool CanDecide(PendingToolApproval approval) =>
        approval.Status is ToolApprovalStatus.Pending or ToolApprovalStatus.Refused or ToolApprovalStatus.Failed;

    private static ToolApprovalStatus StatusFor(ToolApprovalOutcome outcome, ToolApprovalDecision decision) => outcome switch
    {
        ToolApprovalOutcome.Accepted => decision == ToolApprovalDecision.Allow ? ToolApprovalStatus.Allowed : ToolApprovalStatus.Denied,
        ToolApprovalOutcome.Refused => ToolApprovalStatus.Refused,
        ToolApprovalOutcome.AlreadyDecided => ToolApprovalStatus.AlreadyDecided,
        ToolApprovalOutcome.NotFound => ToolApprovalStatus.NotFound,
        ToolApprovalOutcome.Failed => ToolApprovalStatus.Failed,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static string MessageFor(ToolApprovalOutcome outcome, ToolApprovalDecision decision) => outcome switch
    {
        ToolApprovalOutcome.Accepted => decision == ToolApprovalDecision.Allow
            ? "Allowed. The decision is recorded."
            : "Denied. The decision is recorded.",
        ToolApprovalOutcome.Refused => "Policy does not grant this tool.",
        ToolApprovalOutcome.AlreadyDecided => "This request is no longer pending.",
        ToolApprovalOutcome.NotFound => "The session was not found, so the decision was not recorded.",
        ToolApprovalOutcome.Failed => "The decision was not recorded. Try again.",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };
}

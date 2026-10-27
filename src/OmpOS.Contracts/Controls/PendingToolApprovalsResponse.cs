namespace OmpOS.Contracts.Controls;

/// <summary>The answer of <c>GET /sessions/{id}/tool-approvals</c>: the requests that wait for a decision, oldest first.</summary>
public sealed record PendingToolApprovalsResponse
{
    /// <summary>Gets the session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the pending requests.</summary>
    public required IReadOnlyList<PendingToolApproval> Approvals { get; init; }
}

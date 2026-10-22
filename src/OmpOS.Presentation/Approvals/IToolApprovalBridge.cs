namespace OmpOS.Presentation.Approvals;

/// <summary>
/// Sends one approval decision to the server's permission bridge (ADE-02). An implementation maps the server's answer to
/// <see cref="ToolApprovalOutcome"/> and reports <see cref="ToolApprovalOutcome.Accepted"/> only for a decision the server recorded.
/// </summary>
public interface IToolApprovalBridge
{
    /// <summary>Sends one decision for a pending request.</summary>
    /// <param name="sessionId">The session that raised the request.</param>
    /// <param name="requestId">The permission request identifier.</param>
    /// <param name="decision">The user's decision.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The server's outcome for the decision.</returns>
    Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken);
}

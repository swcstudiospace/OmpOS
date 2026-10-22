using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Approvals;

/// <summary>
/// One pending tool-approval request in the run UI (ADE-02). It starts in <see cref="ToolApprovalStatus.Pending"/>. Only
/// <see cref="ToolApprovalCoordinator"/> changes it, and it records each server outcome, so the status reaches
/// <see cref="ToolApprovalStatus.Allowed"/> only after an accepted allow.
/// </summary>
public sealed class PendingToolApproval : ObservableModel
{
    private ToolApprovalStatus _status = ToolApprovalStatus.Pending;
    private string _statusMessage = string.Empty;

    /// <summary>Creates a pending request. It starts pending: nothing is allowed by default.</summary>
    /// <param name="sessionId">The session that raised the request.</param>
    /// <param name="requestId">The server's permission request identifier.</param>
    /// <param name="toolName">The tool that needs approval.</param>
    /// <param name="argumentsSummary">A short summary of the tool arguments for the user.</param>
    /// <exception cref="ArgumentException">An identifier or the tool name is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="argumentsSummary"/> is null.</exception>
    public PendingToolApproval(string sessionId, string requestId, string toolName, string argumentsSummary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(argumentsSummary);

        SessionId = sessionId;
        RequestId = requestId;
        ToolName = toolName;
        ArgumentsSummary = argumentsSummary;
    }

    /// <summary>Gets the session that raised the request.</summary>
    public string SessionId { get; }

    /// <summary>Gets the server's permission request identifier.</summary>
    public string RequestId { get; }

    /// <summary>Gets the tool that needs approval.</summary>
    public string ToolName { get; }

    /// <summary>Gets the short summary of the tool arguments shown to the user.</summary>
    public string ArgumentsSummary { get; }

    /// <summary>Gets the status of the request as the user sees it.</summary>
    public ToolApprovalStatus Status => _status;

    /// <summary>Gets the user-visible message for the current status, or an empty string while the request is pending.</summary>
    public string StatusMessage => _statusMessage;

    /// <summary>Records the status and message that a server outcome maps to.</summary>
    /// <param name="status">The new status.</param>
    /// <param name="message">The user-visible message for the status.</param>
    internal void Record(ToolApprovalStatus status, string message)
    {
        // The message changes first, so a listener that reacts to Status reads the message that belongs to it.
        SetProperty(ref _statusMessage, message, nameof(StatusMessage));
        SetProperty(ref _status, status, nameof(Status));
    }
}

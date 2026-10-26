namespace OmpOS.Presentation.Approvals;

/// <summary>The decision the user makes on one pending tool-approval request (ADE-02).</summary>
public enum ToolApprovalDecision
{
    /// <summary>Allow the tool call. The server decides whether policy grants it.</summary>
    Allow = 0,

    /// <summary>Deny the tool call.</summary>
    Deny = 1,
}

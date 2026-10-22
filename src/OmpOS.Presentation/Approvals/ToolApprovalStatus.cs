namespace OmpOS.Presentation.Approvals;

/// <summary>The state of one tool-approval request as the user sees it (ADE-02).</summary>
public enum ToolApprovalStatus
{
    /// <summary>No decision has been recorded. Nothing is allowed by default.</summary>
    Pending = 0,

    /// <summary>The server accepted an allow decision. Only an accepted allow reaches this status.</summary>
    Allowed = 1,

    /// <summary>The server accepted a deny decision.</summary>
    Denied = 2,

    /// <summary>Policy does not grant the tool. The request stays pending, so the user may still deny it.</summary>
    Refused = 3,

    /// <summary>The request is no longer pending because it was already decided.</summary>
    AlreadyDecided = 4,

    /// <summary>The session does not exist.</summary>
    NotFound = 5,

    /// <summary>The decision was not recorded. The request stays pending and may be decided again.</summary>
    Failed = 6,
}

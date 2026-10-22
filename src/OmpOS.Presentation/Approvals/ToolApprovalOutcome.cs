namespace OmpOS.Presentation.Approvals;

/// <summary>
/// What the server reported for one approval decision (ADE-02). The approval endpoint answers 204 for <see cref="Accepted"/>,
/// 403 for <see cref="Refused"/>, 409 for <see cref="AlreadyDecided"/>, 404 for <see cref="NotFound"/>, and any transport
/// or server failure for <see cref="Failed"/>.
/// </summary>
public enum ToolApprovalOutcome
{
    /// <summary>The server recorded the decision.</summary>
    Accepted = 0,

    /// <summary>Policy does not grant the tool. The request stays pending.</summary>
    Refused = 1,

    /// <summary>The request is not pending: it was already decided, or it is unknown, or the session is not awaiting approval.</summary>
    AlreadyDecided = 2,

    /// <summary>The session does not exist.</summary>
    NotFound = 3,

    /// <summary>The decision was not recorded. The request stays pending.</summary>
    Failed = 4,
}

namespace Autonogrammer.Contracts.Ui;

/// <summary>
/// The decision values a <see cref="PermissionBlock"/> can record. The wire carries a plain string so a decision this
/// version does not know still deserializes; a null decision means the request is undecided.
/// </summary>
public static class ApprovalDecisions
{
    /// <summary>The tool call was allowed.</summary>
    public const string Approved = "approved";

    /// <summary>The tool call was refused.</summary>
    public const string Denied = "denied";
}

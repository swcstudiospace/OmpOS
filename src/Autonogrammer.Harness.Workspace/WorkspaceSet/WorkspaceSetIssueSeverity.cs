namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// How serious a <see cref="WorkspaceSetIssue"/> is.
/// </summary>
public enum WorkspaceSetIssueSeverity
{
    /// <summary>The set works, with a limit the user should know about.</summary>
    Warning,

    /// <summary>The set cannot start a session until the problem is fixed.</summary>
    Error,
}

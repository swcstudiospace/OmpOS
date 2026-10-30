namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// One finding of <see cref="WorkspaceSetValidator"/>.
/// </summary>
/// <param name="Severity">Whether the set can be used.</param>
/// <param name="Code">The kind of problem.</param>
/// <param name="RootId">The root the problem is about, or null when it is about the whole set.</param>
/// <param name="Message">A sentence for the user that names the root and says what to change.</param>
public sealed record WorkspaceSetIssue(WorkspaceSetIssueSeverity Severity, WorkspaceSetIssueCode Code, string? RootId, string Message);

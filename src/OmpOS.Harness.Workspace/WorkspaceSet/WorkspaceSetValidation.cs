namespace OmpOS.Workspace.Sets;

/// <summary>
/// The result of validating a <see cref="WorkspaceSet"/>.
/// </summary>
/// <param name="Issues">Every finding, in root order, errors and warnings together.</param>
public sealed record WorkspaceSetValidation(IReadOnlyList<WorkspaceSetIssue> Issues)
{
    /// <summary>Gets whether the set has no error. Warnings do not make a set invalid.</summary>
    public bool IsValid => !Issues.Any(issue => issue.Severity == WorkspaceSetIssueSeverity.Error);

    /// <summary>Gets the findings that block the set.</summary>
    public IEnumerable<WorkspaceSetIssue> Errors => Issues.Where(issue => issue.Severity == WorkspaceSetIssueSeverity.Error);

    /// <summary>Gets the findings that do not block the set.</summary>
    public IEnumerable<WorkspaceSetIssue> Warnings => Issues.Where(issue => issue.Severity == WorkspaceSetIssueSeverity.Warning);
}

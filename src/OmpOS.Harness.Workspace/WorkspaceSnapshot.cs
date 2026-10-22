namespace OmpOS.Workspace;

/// <summary>
/// The workspace convention state at one point in time. Malformed files are listed in <see cref="Diagnostics"/>
/// and are absent from the other lists.
/// </summary>
/// <param name="Rules">The loaded rules, ordered by id.</param>
/// <param name="Skills">The loaded skills, ordered by id.</param>
/// <param name="Modes">The loaded modes, ordered by file path.</param>
/// <param name="Diagnostics">The malformed files, ordered by file path.</param>
public sealed record WorkspaceSnapshot(
    IReadOnlyList<WorkspaceDocument> Rules,
    IReadOnlyList<WorkspaceDocument> Skills,
    IReadOnlyList<WorkspaceMode> Modes,
    IReadOnlyList<WorkspaceDiagnostic> Diagnostics)
{
    /// <summary>
    /// Gets a snapshot with nothing loaded.
    /// </summary>
    public static WorkspaceSnapshot Empty { get; } = new([], [], [], []);
}

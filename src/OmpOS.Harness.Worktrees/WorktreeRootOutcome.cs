namespace OmpOS.Worktrees;

/// <summary>
/// The result for one root of a run set.
/// </summary>
/// <param name="Name">The root name from the spec.</param>
/// <param name="SourcePath">The folder the root was given as, as a full path.</param>
/// <param name="Status">What happened to the root.</param>
/// <param name="WorkingPath">The directory the run uses for the root: the worktree for <see cref="WorktreeRootStatus.Created"/>, the source folder for <see cref="WorktreeRootStatus.UsedInPlace"/>, and the source folder otherwise.</param>
/// <param name="Run">The worktree run for a <see cref="WorktreeRootStatus.Created"/> root; null for the others.</param>
/// <param name="Detail">A sentence for the user for every status except <see cref="WorktreeRootStatus.Created"/>, and for a created root whose rollback failed, otherwise null.</param>
public sealed record WorktreeRootOutcome(
    string Name,
    string SourcePath,
    WorktreeRootStatus Status,
    string WorkingPath,
    WorktreeRun? Run,
    string? Detail);

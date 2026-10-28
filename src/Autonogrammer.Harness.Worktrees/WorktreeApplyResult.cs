namespace Autonogrammer.Worktrees;

/// <summary>The result of applying a run's patch. An expected failure is a status, never an exception.</summary>
/// <param name="Status">How it ended.</param>
/// <param name="Files">The files the patch changes.</param>
/// <param name="ConflictPaths">The files that conflict, relative to the repository root; empty unless <paramref name="Status"/> is <see cref="WorktreeApplyStatus.Conflicts"/>.</param>
/// <param name="Message">One sentence that says what happened.</param>
public sealed record WorktreeApplyResult(WorktreeApplyStatus Status, IReadOnlyList<WorktreePatchFile> Files, IReadOnlyList<string> ConflictPaths, string Message);

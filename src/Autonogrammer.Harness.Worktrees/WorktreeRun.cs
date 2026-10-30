namespace Autonogrammer.Worktrees;

/// <summary>
/// An agent run that owns a git worktree.
/// </summary>
/// <param name="RunId">The validated run id.</param>
/// <param name="Path">Absolute path of the worktree directory (the operator root joined with <see cref="RunId"/>).</param>
/// <param name="Branch">The branch checked out in the worktree, <c>autonogrammer/run/&lt;RunId&gt;</c>.</param>
public sealed record WorktreeRun(string RunId, string Path, string Branch);

namespace Autonogrammer.Worktrees;

/// <summary>What an <see cref="IWorktreeSetupExecutor"/> runs. Consent was given before it was made.</summary>
/// <param name="WorktreePath">The directory the commands run in.</param>
/// <param name="ProjectRoot">The main checkout, passed to the commands as <c>ROOT_WORKTREE_PATH</c>.</param>
/// <param name="Setup">The commands, or the script.</param>
public sealed record WorktreeSetupExecution(string WorktreePath, string ProjectRoot, CursorWorktreeSetup Setup);

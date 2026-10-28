namespace OmpOS.Worktrees;

/// <summary>A request to run the setup of a new worktree.</summary>
/// <param name="WorktreePath">The new worktree. Setup commands run in it.</param>
/// <param name="ProjectRoot">The main checkout. <c>ROOT_WORKTREE_PATH</c> names it and the configuration is looked for there when the worktree has none.</param>
public sealed record WorktreeSetupRequest(string WorktreePath, string ProjectRoot);

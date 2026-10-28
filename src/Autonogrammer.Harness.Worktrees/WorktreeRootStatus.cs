namespace Autonogrammer.Worktrees;

/// <summary>
/// What happened to one root when a run set was created.
/// </summary>
public enum WorktreeRootStatus
{
    /// <summary>A worktree and a branch were created for the root.</summary>
    Created,

    /// <summary>The root is not the top of a git repository, so the run works in the folder itself, without isolation.</summary>
    UsedInPlace,

    /// <summary>git could not create the worktree. The whole set was rolled back.</summary>
    Failed,

    /// <summary>The worktree was created and then removed again because another root failed.</summary>
    RolledBack,

    /// <summary>The root was not attempted because an earlier root failed.</summary>
    NotAttempted,
}

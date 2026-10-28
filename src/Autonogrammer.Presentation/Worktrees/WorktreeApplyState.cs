namespace Autonogrammer.Presentation.Worktrees;

/// <summary>How applying a run's patch ended.</summary>
public enum WorktreeApplyState
{
    /// <summary>Every change was applied.</summary>
    Applied = 0,

    /// <summary>The patch does not fit; <see cref="WorktreeApplyOutcome.ConflictPaths"/> names the files.</summary>
    Conflicts = 1,

    /// <summary>The run changed nothing.</summary>
    NothingToApply = 2,

    /// <summary>Git failed; nothing was written.</summary>
    Failed = 3,
}

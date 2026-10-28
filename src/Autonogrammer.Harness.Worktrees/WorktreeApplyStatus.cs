namespace Autonogrammer.Worktrees;

/// <summary>How applying a run's patch ended.</summary>
public enum WorktreeApplyStatus
{
    /// <summary>Every change was applied.</summary>
    Applied = 0,

    /// <summary>The patch does not fit the checkout. With <see cref="WorktreeApplyMode.Clean"/> nothing was written; with <see cref="WorktreeApplyMode.ThreeWay"/> the conflicting files hold conflict markers.</summary>
    Conflicts = 1,

    /// <summary>The run changed nothing, so there was nothing to apply.</summary>
    NothingToApply = 2,

    /// <summary>Git failed for another reason. Nothing was written.</summary>
    Failed = 3,
}

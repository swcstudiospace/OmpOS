namespace Autonogrammer.Worktrees;

/// <summary>How a run's patch is applied to the checkout.</summary>
public enum WorktreeApplyMode
{
    /// <summary>The patch is applied only when it fits the checkout exactly. Otherwise nothing is written and the files that do not fit are listed.</summary>
    Clean = 0,

    /// <summary>When the patch does not fit, git merges it three ways and leaves conflict markers in the files that conflict. The merged changes are staged.</summary>
    ThreeWay = 1,
}

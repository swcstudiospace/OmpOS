namespace OmpOS.Presentation.Worktrees;

/// <summary>How a run's patch is applied.</summary>
public enum WorktreeApplyMethod
{
    /// <summary>Only when the patch fits exactly; otherwise nothing is written and the files that do not fit are listed.</summary>
    Clean = 0,

    /// <summary>Merge three ways when the patch does not fit; files that conflict get conflict markers.</summary>
    ThreeWay = 1,
}

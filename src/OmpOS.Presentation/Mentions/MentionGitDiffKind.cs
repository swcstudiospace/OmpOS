namespace OmpOS.Presentation.Mentions;

/// <summary>Which diff a git mention reads.</summary>
public enum MentionGitDiffKind
{
    /// <summary>The working state: uncommitted changes against the last commit.</summary>
    Working = 0,

    /// <summary>The branch against its base branch.</summary>
    Branch = 1,
}

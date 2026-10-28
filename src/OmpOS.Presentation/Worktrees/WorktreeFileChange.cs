namespace OmpOS.Presentation.Worktrees;

/// <summary>What a run did to one file.</summary>
public enum WorktreeFileChange
{
    /// <summary>The run created the file.</summary>
    Added = 0,

    /// <summary>The run changed the file.</summary>
    Modified = 1,

    /// <summary>The run deleted the file.</summary>
    Deleted = 2,

    /// <summary>Another change, such as a type change.</summary>
    Other = 3,
}

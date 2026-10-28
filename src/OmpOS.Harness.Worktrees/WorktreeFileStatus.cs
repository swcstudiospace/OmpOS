namespace OmpOS.Worktrees;

/// <summary>What a run did to one file, as git reports it against the commit the run started from.</summary>
public enum WorktreeFileStatus
{
    /// <summary>The run created the file.</summary>
    Added = 0,

    /// <summary>The run changed the file.</summary>
    Modified = 1,

    /// <summary>The run deleted the file.</summary>
    Deleted = 2,

    /// <summary>Another change git reports, such as a type change.</summary>
    Other = 3,
}

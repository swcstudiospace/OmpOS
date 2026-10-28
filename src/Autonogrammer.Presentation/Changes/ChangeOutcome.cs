namespace Autonogrammer.Presentation.Changes;

/// <summary>The outcome of one Keep, Undo or review action on one changed file.</summary>
public enum ChangeOutcome
{
    /// <summary>The user kept the edits.</summary>
    Kept,

    /// <summary>The edits were undone on disk, or the file the agent created was removed.</summary>
    Undone,

    /// <summary>Some hunks of the edits were undone on disk and the others stay.</summary>
    PartlyUndone,

    /// <summary>The file changed after the agent edited it. Nothing was written.</summary>
    Stale,

    /// <summary>The path resolves outside the workspace, or no file system reaches it. Nothing was written.</summary>
    Refused,

    /// <summary>The file could not be read or written. Nothing was written.</summary>
    Failed,

    /// <summary>The path names no changed file, or the file was already decided. Nothing was done.</summary>
    NotApplicable,
}

namespace OmpOS.Presentation.Checkpoints;

/// <summary>How a rewind ended.</summary>
public enum CheckpointRewindStatus
{
    /// <summary>The files were written back.</summary>
    Completed = 0,

    /// <summary>A file is stale and the rewind was not forced, so nothing was written.</summary>
    Blocked = 1,

    /// <summary>A write failed; files already written were put back.</summary>
    Failed = 2,

    /// <summary>There was nothing to write.</summary>
    NothingToDo = 3,

    /// <summary>The checkpoint no longer exists.</summary>
    UnknownTurn = 4,
}

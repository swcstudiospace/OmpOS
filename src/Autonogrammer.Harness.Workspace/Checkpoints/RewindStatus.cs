namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>How a rewind ended.</summary>
public enum RewindStatus
{
    /// <summary>Every file the plan said to write or delete was written or deleted.</summary>
    Completed = 0,

    /// <summary>A file is stale and the rewind was not forced, so nothing was written.</summary>
    Blocked = 1,

    /// <summary>A write failed. Files already written were put back, so the workspace is as it was.</summary>
    Failed = 2,

    /// <summary>Every file already holds its earlier content, or only refused and unrestorable files remain.</summary>
    NothingToDo = 3,

    /// <summary>The session has no such turn.</summary>
    UnknownTurn = 4,
}

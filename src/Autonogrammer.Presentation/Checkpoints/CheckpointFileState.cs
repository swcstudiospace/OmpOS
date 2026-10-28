namespace Autonogrammer.Presentation.Checkpoints;

/// <summary>What a rewind does to one file.</summary>
public enum CheckpointFileState
{
    /// <summary>The file is written back with its content from before the turn.</summary>
    Restore = 0,

    /// <summary>The turn created the file, so the rewind deletes it.</summary>
    Delete = 1,

    /// <summary>The file already holds its earlier content.</summary>
    AlreadyAtTarget = 2,

    /// <summary>The file changed after the agent's last edit: a rewind that is not forced writes nothing.</summary>
    Stale = 3,

    /// <summary>The file is outside the open workspace folders, so it is never touched.</summary>
    Refused = 4,

    /// <summary>The earlier content was not kept (binary or too large), so the file is left as it is.</summary>
    Unrestorable = 5,
}

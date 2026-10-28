namespace OmpOS.Workspace.Checkpoints;

/// <summary>What a rewind does, or would do, to one file.</summary>
public enum RewindFileAction
{
    /// <summary>The file is written back with its content from before the turn.</summary>
    Restore = 0,

    /// <summary>The turn created the file, so the rewind deletes it.</summary>
    Delete = 1,

    /// <summary>The file already holds its content from before the turn, so nothing is written.</summary>
    AlreadyAtTarget = 2,

    /// <summary>The file changed since the turn, so a rewind without force leaves everything alone. A forced rewind overwrites it.</summary>
    Stale = 3,

    /// <summary>The file is outside the open workspace folders, or its folder is not open now, so it is never touched.</summary>
    Refused = 4,

    /// <summary>The earlier content was not kept (binary or too large) or its stored copy is damaged, so the file is left as it is.</summary>
    Unrestorable = 5,
}

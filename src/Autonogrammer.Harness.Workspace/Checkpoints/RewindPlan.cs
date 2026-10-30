namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>What rewinding to before one turn would do. Computing it writes nothing.</summary>
/// <param name="SessionId">The session.</param>
/// <param name="TurnId">The turn the files go back to the state before.</param>
/// <param name="Files">One entry per file that this turn or a later turn changed.</param>
public sealed record RewindPlan(string SessionId, int TurnId, IReadOnlyList<RewindFilePlan> Files)
{
    /// <summary>Gets the number of files a rewind would write or delete.</summary>
    public int ChangeCount => Files.Count(static file => file.Action is RewindFileAction.Restore or RewindFileAction.Delete);

    /// <summary>Gets the number of files that changed since the turn and block a rewind that is not forced.</summary>
    public int StaleCount => Files.Count(static file => file.Action == RewindFileAction.Stale);

    /// <summary>Gets the number of files a rewind never touches: refused and unrestorable ones.</summary>
    public int SkippedCount => Files.Count(static file => file.Action is RewindFileAction.Refused or RewindFileAction.Unrestorable);

    /// <summary>Gets whether a rewind that is not forced would write nothing because a file is stale.</summary>
    public bool IsBlocked => StaleCount > 0;
}

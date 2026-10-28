namespace OmpOS.Presentation.Checkpoints;

/// <summary>What a rewind to before one turn would do.</summary>
/// <param name="TurnId">The turn the files go back to the state before.</param>
/// <param name="Files">One entry per file that this turn or a later turn changed.</param>
public sealed record CheckpointRewindPreview(int TurnId, IReadOnlyList<CheckpointFilePreview> Files)
{
    /// <summary>Gets the number of files the rewind would write or delete.</summary>
    public int ChangeCount => Files.Count(static file => file.State is CheckpointFileState.Restore or CheckpointFileState.Delete);

    /// <summary>Gets the number of files that changed since the agent's edit.</summary>
    public int StaleCount => Files.Count(static file => file.State == CheckpointFileState.Stale);

    /// <summary>Gets the number of files the rewind never touches.</summary>
    public int SkippedCount => Files.Count(static file => file.State is CheckpointFileState.Refused or CheckpointFileState.Unrestorable);
}

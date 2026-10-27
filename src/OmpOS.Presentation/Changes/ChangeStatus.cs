namespace OmpOS.Presentation.Changes;

/// <summary>Where one changed file stands in the review of a session's changes.</summary>
public enum ChangeStatus
{
    /// <summary>The agent's edits are on disk and nobody has decided about them yet.</summary>
    Pending,

    /// <summary>The user kept the agent's edits. Nothing was written.</summary>
    Kept,

    /// <summary>The user undid the agent's edits: the file holds its text from before the agent, or is gone when the agent created it.</summary>
    Undone,

    /// <summary>The file changed on disk after the agent edited it, so an undo was refused and nothing was written.</summary>
    Stale,
}

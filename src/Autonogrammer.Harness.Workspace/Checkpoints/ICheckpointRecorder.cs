namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>
/// Where the app reports what each agent turn changed. The Changes pipeline of the presentation layer feeds it after every transcript update, so checkpoints
/// are recorded without the user doing anything. Recording the same turn key again replaces that turn's files, because a turn's net edit grows while it streams.
/// </summary>
public interface ICheckpointRecorder
{
    /// <summary>Records, or replaces, the files one turn changed.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="turnKey">A stable key of the turn, for example the id of its user message. A turn the user rewound is never recorded again.</param>
    /// <param name="label">A short description of the turn, for example its prompt; may be empty.</param>
    /// <param name="changes">The turn's net changes, one per file.</param>
    /// <param name="cancellationToken">Cancels the recording.</param>
    /// <returns>The recorded turn, or <see langword="null"/> when nothing was recorded: no net change, only files outside the workspace, or a rewound turn.</returns>
    Task<CheckpointTurn?> RecordTurnAsync(string sessionId, string turnKey, string? label, IReadOnlyList<CheckpointFileChange> changes, CancellationToken cancellationToken);
}

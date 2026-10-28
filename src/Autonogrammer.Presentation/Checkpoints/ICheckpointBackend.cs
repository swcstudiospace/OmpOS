namespace Autonogrammer.Presentation.Checkpoints;

/// <summary>
/// The file checkpoints of the host. The desktop head implements it over <c>CheckpointStore</c> of <c>Autonogrammer.Harness.Workspace</c>; presentation never references that library.
/// Recording is fed by <see cref="CheckpointRecorderModel"/> and the timeline is shown by <see cref="CheckpointsModel"/>. A rewind restores files only and never changes the conversation.
/// </summary>
public interface ICheckpointBackend
{
    /// <summary>Records, or replaces, the files one turn changed.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="turn">The turn and its net file edits.</param>
    /// <param name="cancellationToken">Cancels the recording.</param>
    /// <returns>A task that completes when the turn is kept.</returns>
    Task RecordTurnAsync(string sessionId, CheckpointTurnEdits turn, CancellationToken cancellationToken);

    /// <summary>Lists the turns of a session that changed files.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The turns, oldest first.</returns>
    Task<IReadOnlyList<CheckpointTurnInfo>> ListAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>Works out what a rewind to before a turn would do, without writing anything.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="turnId">The turn.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The preview.</returns>
    Task<CheckpointRewindPreview> PreviewAsync(string sessionId, int turnId, CancellationToken cancellationToken);

    /// <summary>Puts the files back to the state before a turn.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="turnId">The turn.</param>
    /// <param name="force"><see langword="true"/> overwrites files that changed after the agent's last edit.</param>
    /// <param name="cancellationToken">Cancels the rewind before it writes.</param>
    /// <returns>How it ended. An expected failure is a status, never an exception.</returns>
    Task<CheckpointRewindOutcome> RewindAsync(string sessionId, int turnId, bool force, CancellationToken cancellationToken);
}

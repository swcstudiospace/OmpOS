using Autonogrammer.Presentation.Checkpoints;

namespace Autonogrammer.Native.Tests.Checkpoints;

/// <summary>A checkpoint backend that keeps the rewinds it was asked for and answers from scripted data. Every call completes at once.</summary>
internal sealed class ScriptedCheckpointBackend : ICheckpointBackend
{
    /// <summary>Gets or sets the turns <see cref="ListAsync"/> returns.</summary>
    public List<CheckpointTurnInfo> Turns { get; set; } = [];

    /// <summary>Gets or sets the files <see cref="PreviewAsync"/> and <see cref="RewindAsync"/> report.</summary>
    public IReadOnlyList<CheckpointFilePreview> PreviewFiles { get; set; } = [];

    /// <summary>Gets or sets the exception every call throws, or null.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Gets the rewinds that were requested as (turn, force).</summary>
    public List<(int TurnId, bool Force)> Rewinds { get; } = [];

    /// <inheritdoc/>
    public Task RecordTurnAsync(string sessionId, CheckpointTurnEdits turn, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task<IReadOnlyList<CheckpointTurnInfo>> ListAsync(string sessionId, CancellationToken cancellationToken) =>
        Failure is not null ? Task.FromException<IReadOnlyList<CheckpointTurnInfo>>(Failure) : Task.FromResult<IReadOnlyList<CheckpointTurnInfo>>(Turns);

    /// <inheritdoc/>
    public Task<CheckpointRewindPreview> PreviewAsync(string sessionId, int turnId, CancellationToken cancellationToken) =>
        Failure is not null ? Task.FromException<CheckpointRewindPreview>(Failure) : Task.FromResult(new CheckpointRewindPreview(turnId, PreviewFiles));

    /// <inheritdoc/>
    public Task<CheckpointRewindOutcome> RewindAsync(string sessionId, int turnId, bool force, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromException<CheckpointRewindOutcome>(Failure);
        }

        Rewinds.Add((turnId, force));
        return Task.FromResult(new CheckpointRewindOutcome(CheckpointRewindStatus.Completed, new CheckpointRewindPreview(turnId, PreviewFiles), "Restored 1 file."));
    }
}

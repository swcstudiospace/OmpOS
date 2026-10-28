using Autonogrammer.Presentation.Checkpoints;

namespace Autonogrammer.Presentation.Tests.Checkpoints;

/// <summary>A checkpoint backend that keeps what it was given and answers from scripted data.</summary>
internal sealed class FakeCheckpointBackend : ICheckpointBackend
{
    /// <summary>Gets the turns handed to <see cref="RecordTurnAsync"/>, in order.</summary>
    public List<CheckpointTurnEdits> Recorded { get; } = [];

    /// <summary>Gets or sets the turns <see cref="ListAsync"/> returns.</summary>
    public List<CheckpointTurnInfo> Turns { get; set; } = [];

    /// <summary>Gets or sets the preview <see cref="PreviewAsync"/> returns, with the requested turn id.</summary>
    public IReadOnlyList<CheckpointFilePreview> PreviewFiles { get; set; } = [];

    /// <summary>Gets or sets the status <see cref="RewindAsync"/> returns.</summary>
    public CheckpointRewindStatus RewindStatus { get; set; } = CheckpointRewindStatus.Completed;

    /// <summary>Gets or sets the exception the next call throws, or null.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Gets the rewinds that were requested as (turn, force).</summary>
    public List<(int TurnId, bool Force)> Rewinds { get; } = [];

    /// <inheritdoc/>
    public Task RecordTurnAsync(string sessionId, CheckpointTurnEdits turn, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        Recorded.Add(turn);
        return Task.CompletedTask;
    }

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
        return Task.FromResult(new CheckpointRewindOutcome(RewindStatus, new CheckpointRewindPreview(turnId, PreviewFiles), RewindStatus == CheckpointRewindStatus.Completed ? "Restored 1 file." : "Not done."));
    }
}

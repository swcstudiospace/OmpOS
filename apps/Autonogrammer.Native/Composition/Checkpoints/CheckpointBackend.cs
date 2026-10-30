using Autonogrammer.Presentation.Checkpoints;
using Autonogrammer.Workspace.Checkpoints;

namespace Autonogrammer.Native.Composition.Checkpoints;

/// <summary>
/// The desktop implementation of <see cref="ICheckpointBackend"/> over a <see cref="CheckpointStore"/>. The store belongs to the window that created it, so the window disposes it, not this adapter.
/// Enumerations of the two layers are mapped member by member, never by their numbers.
/// </summary>
internal sealed class CheckpointBackend : ICheckpointBackend
{
    private readonly CheckpointStore _store;

    /// <summary>Initializes a new instance of the <see cref="CheckpointBackend"/> class.</summary>
    /// <param name="store">The store that keeps the checkpoints of the window.</param>
    public CheckpointBackend(CheckpointStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public async Task RecordTurnAsync(string sessionId, CheckpointTurnEdits turn, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(turn);
        var changes = new List<CheckpointFileChange>(turn.Files.Count);
        foreach (var file in turn.Files)
        {
            changes.Add(CheckpointFileChange.FromText(file.Path, file.OldText, file.NewText));
        }

        await _store.RecordTurnAsync(sessionId, turn.TurnKey, turn.Label, changes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CheckpointTurnInfo>> ListAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        var turns = await _store.ListAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var result = new List<CheckpointTurnInfo>(turns.Count);
        foreach (var turn in turns)
        {
            result.Add(new CheckpointTurnInfo(turn.TurnId, turn.Label, turn.RecordedAt, [.. turn.Files.Select(static file => file.Path)]));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<CheckpointRewindPreview> PreviewAsync(string sessionId, int turnId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        var plan = await _store.PlanRewindAsync(sessionId, turnId, cancellationToken).ConfigureAwait(false);
        return ToPreview(plan);
    }

    /// <inheritdoc />
    public async Task<CheckpointRewindOutcome> RewindAsync(string sessionId, int turnId, bool force, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        var result = await _store.RewindAsync(sessionId, turnId, force, cancellationToken).ConfigureAwait(false);
        return new CheckpointRewindOutcome(ToStatus(result.Status), ToPreview(result.Plan), result.Message);
    }

    private static CheckpointRewindPreview ToPreview(RewindPlan plan) =>
        new(plan.TurnId, [.. plan.Files.Select(static file => new CheckpointFilePreview(file.Path, ToState(file.Action), file.Detail))]);

    private static CheckpointFileState ToState(RewindFileAction action) => action switch
    {
        RewindFileAction.Restore => CheckpointFileState.Restore,
        RewindFileAction.Delete => CheckpointFileState.Delete,
        RewindFileAction.AlreadyAtTarget => CheckpointFileState.AlreadyAtTarget,
        RewindFileAction.Stale => CheckpointFileState.Stale,
        RewindFileAction.Refused => CheckpointFileState.Refused,
        RewindFileAction.Unrestorable => CheckpointFileState.Unrestorable,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown rewind file action."),
    };

    private static CheckpointRewindStatus ToStatus(RewindStatus status) => status switch
    {
        RewindStatus.Completed => CheckpointRewindStatus.Completed,
        RewindStatus.Blocked => CheckpointRewindStatus.Blocked,
        RewindStatus.Failed => CheckpointRewindStatus.Failed,
        RewindStatus.NothingToDo => CheckpointRewindStatus.NothingToDo,
        RewindStatus.UnknownTurn => CheckpointRewindStatus.UnknownTurn,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown rewind status."),
    };
}

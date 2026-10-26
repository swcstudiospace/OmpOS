namespace OmpOS.Contracts.Agents;

/// <summary>
/// A source of <see cref="StageEvent"/>s for one session. An implementation observes a plugin through its side channels
/// (files, HTTP) and never throws for a missing or malformed input: it reports that as a <see cref="StageStatus.Degraded"/>
/// or <see cref="StageStatus.Unavailable"/> event and keeps observing.
/// </summary>
public interface IStageSource
{
    /// <summary>Observes the stages of the session named by <paramref name="scope"/> until <paramref name="cancellationToken"/> is cancelled.</summary>
    /// <param name="scope">The session to observe.</param>
    /// <param name="cancellationToken">Ends the observation. Cancellation ends the sequence without an exception being thrown to the consumer's loop body.</param>
    /// <returns>The events in the order they were observed.</returns>
    IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, CancellationToken cancellationToken);
}

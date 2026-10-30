using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>
/// Records that the operator steered a session into the Substrate ledger. Recording is an audit side effect: Autonogrammer delivers the
/// steering itself and never claims the Substrate delivered it.
/// </summary>
public interface ISteeringRecorder
{
    /// <summary>Records one steering action.</summary>
    /// <param name="scope">The session that was steered.</param>
    /// <param name="record">What to record. It carries a length and a hash prefix, never the operator text.</param>
    /// <param name="cancellationToken">Cancels the recording.</param>
    /// <returns>The outcome. The call does not throw.</returns>
    Task<SteeringRecordResult> RecordAsync(StageObservationScope scope, SteeringRecord record, CancellationToken cancellationToken);
}

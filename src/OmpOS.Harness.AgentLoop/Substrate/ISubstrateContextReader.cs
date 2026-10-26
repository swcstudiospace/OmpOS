using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>
/// Reads the shared memory, lessons and task facts the Substrate holds for one session. The read is bound to the session's scope:
/// it asks only about the session's own graph and the repository of its workspace, and every part that fails becomes a warning
/// in the snapshot instead of an exception.
/// </summary>
public interface ISubstrateContextReader
{
    /// <summary>Reads the context of the session named by <paramref name="scope"/>.</summary>
    /// <param name="scope">The session to read for.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot. It is never null and the call does not throw for an unreachable or misbehaving server.</returns>
    Task<SubstrateContextSnapshot> ReadAsync(StageObservationScope scope, CancellationToken cancellationToken);
}

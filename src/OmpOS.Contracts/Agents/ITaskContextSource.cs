namespace OmpOS.Contracts.Agents;

/// <summary>
/// Supplies the <see cref="TaskContext"/> of a session. An implementation returns whatever parts it can read and lists the rest as warnings.
/// </summary>
public interface ITaskContextSource
{
    /// <summary>Reads the context of the session named by <paramref name="scope"/>.</summary>
    /// <param name="scope">The session to describe. A source reads only state that belongs to this scope.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The context. It is never null, and it reports unreadable parts as warnings instead of throwing.</returns>
    Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken);
}

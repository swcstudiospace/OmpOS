namespace OmpOS.Coordination;

/// <summary>
/// Grants at most one live run lease per session. Leases expire on their own, so a crashed owner cannot block a session forever.
/// </summary>
public interface ISessionRunLeases
{
    /// <summary>Attempts to take the exclusive run lease for a session.</summary>
    /// <param name="sessionId">The session to lease.</param>
    /// <param name="ownerId">The identity of the caller that will hold the lease.</param>
    /// <param name="timeToLive">How long the lease lasts unless renewed. Must be at least one millisecond.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The acquired lease, or <see langword="null"/> when another owner holds an unexpired lease.</returns>
    Task<ISessionRunLease?> TryAcquireAsync(string sessionId, string ownerId, TimeSpan timeToLive, CancellationToken cancellationToken);

    /// <summary>Reports whether an unexpired lease exists for a session, whoever holds it.</summary>
    /// <param name="sessionId">The session to inspect.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when a live lease exists.</returns>
    Task<bool> IsHeldAsync(string sessionId, CancellationToken cancellationToken);
}

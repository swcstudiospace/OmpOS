namespace Autonogrammer.EngineHost;

/// <summary>
/// Lists and discards crash-recoverable sessions. v1 ships a no-op default
/// (<see cref="NoOpSessionRecovery"/>); durable recovery is a later shell task.
/// </summary>
public interface ISessionRecovery
{
    /// <summary>Lists the identifiers of sessions recoverable after a crash.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    Task<IReadOnlyList<string>> ListRecoverableSessionIdsAsync(CancellationToken cancellationToken);

    /// <summary>Discards a recoverable session so it is never offered again.</summary>
    /// <param name="sessionId">The session identifier to discard.</param>
    /// <param name="cancellationToken">Cancels the discard.</param>
    Task DiscardAsync(string sessionId, CancellationToken cancellationToken);
}

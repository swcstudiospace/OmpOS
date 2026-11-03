namespace Autonogrammer.Dispatch.Desktop;

/// <summary>Receives the revocation of this device: it ends every session the desktop started for Dispatch.</summary>
public interface IDispatchRevocationSink
{
    /// <summary>Ends the dispatched sessions. Called once, when the hub revokes the device or refuses its credential as revoked.</summary>
    /// <param name="cancellationToken">Cancels the cleanup.</param>
    /// <returns>A task that completes when the sessions are ended.</returns>
    Task EndDispatchedSessionsAsync(CancellationToken cancellationToken);
}

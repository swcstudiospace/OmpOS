namespace OmpOS.EngineHost;

/// <summary>
/// Ensures only one shell instance owns the local engine session. v1 ships a no-op default
/// (<see cref="NoOpSingleInstanceGuard"/>); real single-instance handoff is a later shell task.
/// </summary>
public interface ISingleInstanceGuard
{
    /// <summary>Tries to acquire the single-instance lock.</summary>
    /// <param name="cancellationToken">Cancels the acquisition.</param>
    /// <returns>True when this instance owns the lock.</returns>
    Task<bool> TryAcquireAsync(CancellationToken cancellationToken);

    /// <summary>Releases the single-instance lock, if held.</summary>
    /// <param name="cancellationToken">Cancels the release.</param>
    Task ReleaseAsync(CancellationToken cancellationToken);
}

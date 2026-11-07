namespace Autonogrammer.Accounts;

/// <summary>
/// A seam around every refresh. A desktop head supplies a guard that holds a cross-process lock, so two app instances never spend the same refresh
/// token; <see cref="AccountSessionManager"/> re-reads the secret store once the guard is acquired and adopts a refresh token another process stored.
/// The default guard locks nothing and is right for one process.
/// </summary>
public interface IRefreshGuard
{
    /// <summary>Waits for the right to refresh.</summary>
    /// <param name="cancellationToken">Abandons the wait.</param>
    /// <returns>A handle; disposing it releases the right.</returns>
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken);
}

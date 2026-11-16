using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Presentation.Landing;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// The Code landing's managed omp operations: the check made after first paint, and the install a user starts from the Local machine card. It
/// reads the release, installs it, reports each stage, and maps the launcher's outcome to the presentation record. Nothing here starts an omp
/// process; a running omp is never stopped.
/// </summary>
internal sealed class LocalOmpInstaller
{
    private readonly IOmpManagedInstall _managed;

    /// <summary>Creates the operations over a managed install. The install's owner disposes it.</summary>
    /// <param name="managed">The managed install.</param>
    /// <exception cref="ArgumentNullException"><paramref name="managed"/> is null.</exception>
    public LocalOmpInstaller(IOmpManagedInstall managed)
    {
        ArgumentNullException.ThrowIfNull(managed);
        _managed = managed;
    }

    /// <summary>Checks the managed omp for a newer release. A failure is returned as its notice, never thrown, except for cancellation.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The notice the landing shows.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<LocalOmpUpdate> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            return Map(await _managed.CheckForUpdateAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not OutOfMemoryException)
        {
            return new LocalOmpUpdate(LocalOmpUpdateState.Failed, null, null, exception.GetBaseException().Message);
        }
    }

    /// <summary>
    /// Installs the latest managed omp. <paramref name="progress"/> receives the status at each stage: reading the release, then installing it with
    /// its version. The install itself is the launcher's; it removes its staging directory when it is cancelled.
    /// </summary>
    /// <param name="progress">Receives each stage. It is called on the thread that runs the install.</param>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <returns>
    /// <see cref="LocalOmpUpdateState.Updated"/> when the release is now the managed install; otherwise the state and reason that say why it is not.
    /// </returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="progress"/> is null.</exception>
    public async Task<LocalOmpUpdate> InstallAsync(Action<LocalOmpStatus> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        progress(LocalOmpStatus.Installing(LocalOmpInstallStage.ReadingRelease, null));
        OmpRelease release;
        try
        {
            release = await _managed.ReadReleaseAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Offline();
        }
        catch (HttpRequestException)
        {
            return Offline();
        }
        catch (InvalidDataException exception)
        {
            return new LocalOmpUpdate(LocalOmpUpdateState.Failed, null, null, OmpManagedMessages.InvalidRegistryResponse(exception.Message));
        }

        progress(LocalOmpStatus.Installing(LocalOmpInstallStage.Installing, release.Version));
        return Map(await _managed.InstallAsync(release, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Maps a launcher outcome to the notice the landing shows.</summary>
    /// <param name="result">The outcome.</param>
    /// <returns>The notice.</returns>
    internal static LocalOmpUpdate Map(OmpUpdateResult result) => result switch
    {
        OmpUpdated updated => new LocalOmpUpdate(LocalOmpUpdateState.Updated, updated.Version, null, updated.Message),
        OmpUpToDate upToDate => new LocalOmpUpdate(LocalOmpUpdateState.UpToDate, upToDate.Version, upToDate.CheckedAt, upToDate.Message),
        OmpOffline offline => new LocalOmpUpdate(LocalOmpUpdateState.Offline, offline.StaleVersion, offline.LastCheckedAt, offline.Message),
        OmpDeferred deferred => new LocalOmpUpdate(LocalOmpUpdateState.Deferred, deferred.Version, null, deferred.Message),
        OmpRolledBack rolledBack => new LocalOmpUpdate(LocalOmpUpdateState.RolledBack, rolledBack.RestoredVersion, null, rolledBack.Message),
        OmpBroken broken => new LocalOmpUpdate(LocalOmpUpdateState.Failed, broken.Version, null, broken.Message),
        _ => new LocalOmpUpdate(LocalOmpUpdateState.Failed, null, null, result.Reason),
    };

    private static LocalOmpUpdate Offline() =>
        new(LocalOmpUpdateState.Offline, null, null, OmpManagedMessages.Offline(version: null, checkedAt: null));
}

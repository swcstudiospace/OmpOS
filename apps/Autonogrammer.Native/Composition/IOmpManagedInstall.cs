using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Native.Composition;

/// <summary>
/// The managed omp install's update and install operations, as the Code landing uses them. The launcher's installer is sealed, so the landing
/// reaches it through this seam, and tests substitute a fake.
/// </summary>
internal interface IOmpManagedInstall
{
    /// <summary>Compares the active managed omp with the registry's latest and installs the latest when it is newer.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The launcher's outcome.</returns>
    Task<OmpUpdateResult> CheckForUpdateAsync(CancellationToken cancellationToken);

    /// <summary>Reads the release the registry publishes as the latest omp.</summary>
    /// <param name="requestedVersion">The version to read, or null for the latest.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The release.</returns>
    Task<OmpRelease> ReadReleaseAsync(string? requestedVersion, CancellationToken cancellationToken);

    /// <summary>Installs a release read by <see cref="ReadReleaseAsync"/> and activates it when it passes its probes.</summary>
    /// <param name="release">The release.</param>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <returns>The launcher's outcome.</returns>
    Task<OmpUpdateResult> InstallAsync(OmpRelease release, CancellationToken cancellationToken);
}

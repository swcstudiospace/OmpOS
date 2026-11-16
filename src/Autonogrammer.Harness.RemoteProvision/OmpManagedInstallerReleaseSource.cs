using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>Reads the latest release through the desktop's managed installer, so the registry address and the network timeout are the ones the desktop uses.</summary>
public sealed class OmpManagedInstallerReleaseSource : IOmpReleaseSource
{
    private readonly OmpManagedInstaller _installer;

    /// <summary>Initializes a new instance of the <see cref="OmpManagedInstallerReleaseSource"/> class.</summary>
    /// <param name="installer">The desktop's installer, built with the managed options of this host.</param>
    /// <exception cref="ArgumentNullException"><paramref name="installer"/> is null.</exception>
    public OmpManagedInstallerReleaseSource(OmpManagedInstaller installer)
    {
        ArgumentNullException.ThrowIfNull(installer);
        _installer = installer;
    }

    /// <inheritdoc />
    public Task<OmpRelease> ReadLatestAsync(CancellationToken cancellationToken) => _installer.ReadReleaseAsync(requestedVersion: null, cancellationToken);
}

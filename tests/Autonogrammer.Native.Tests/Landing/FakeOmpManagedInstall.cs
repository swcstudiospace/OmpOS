using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Native.Composition;

namespace Autonogrammer.Native.Tests.Landing;

/// <summary>A managed omp install that records its calls and returns what each test scripts. Nothing reaches the network or the disk.</summary>
internal sealed class FakeOmpManagedInstall : IOmpManagedInstall
{
    /// <summary>Gets or sets the check. It returns a newer install by default.</summary>
    public Func<CancellationToken, Task<OmpUpdateResult>> Check { get; set; } = _ => Task.FromResult<OmpUpdateResult>(new OmpUpdated("2.0.0", null, "Installed."));

    /// <summary>Gets or sets the release read. It must be scripted before a read.</summary>
    public Func<CancellationToken, Task<OmpRelease>> ReadRelease { get; set; } = _ => throw new InvalidOperationException("No release was scripted.");

    /// <summary>Gets or sets the install. It must be scripted before an install.</summary>
    public Func<OmpRelease, CancellationToken, Task<OmpUpdateResult>> Install { get; set; } = (_, _) => throw new InvalidOperationException("No install was scripted.");

    /// <summary>Gets the version each release read asked for; null is the latest.</summary>
    public List<string?> ReadRequests { get; } = [];

    /// <summary>Gets the release each install received, in order.</summary>
    public List<OmpRelease> Installs { get; } = [];

    /// <inheritdoc />
    public Task<OmpUpdateResult> CheckForUpdateAsync(CancellationToken cancellationToken) => Check(cancellationToken);

    /// <inheritdoc />
    public Task<OmpRelease> ReadReleaseAsync(string? requestedVersion, CancellationToken cancellationToken)
    {
        ReadRequests.Add(requestedVersion);
        return ReadRelease(cancellationToken);
    }

    /// <inheritdoc />
    public Task<OmpUpdateResult> InstallAsync(OmpRelease release, CancellationToken cancellationToken)
    {
        Installs.Add(release);
        return Install(release, cancellationToken);
    }

    /// <summary>Gets a release of the given version, with the registry's address for it.</summary>
    /// <param name="version">The version.</param>
    /// <returns>The release.</returns>
    public static OmpRelease ReleaseOf(string version) =>
        new(version, new Uri("https://registry.npmjs.org/@oh-my-pi/pi-coding-agent/-/pi-coding-agent-" + version + ".tgz"), new string('a', 128), EnginesBun: null);
}

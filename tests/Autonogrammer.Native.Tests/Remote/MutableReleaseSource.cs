using Autonogrammer.Harness.OmpLauncher;
using Autonogrammer.Harness.RemoteProvision;

namespace Autonogrammer.Native.Tests.Remote;

/// <summary>A release source whose release a test can move between the check and the install.</summary>
internal sealed class MutableReleaseSource(OmpRelease release) : IOmpReleaseSource
{
    /// <summary>Gets or sets the release the next read returns.</summary>
    public OmpRelease Release { get; set; } = release;

    /// <inheritdoc />
    public Task<OmpRelease> ReadLatestAsync(CancellationToken cancellationToken) => Task.FromResult(Release);
}

using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.RemoteProvision.Tests;

/// <summary>A release source that returns a fixed release or throws a fixed failure, and counts its reads.</summary>
internal sealed class FakeReleaseSource(OmpRelease? release, Exception? failure = null) : IOmpReleaseSource
{
    /// <summary>Gets the number of reads made.</summary>
    public int Reads { get; private set; }

    /// <inheritdoc />
    public Task<OmpRelease> ReadLatestAsync(CancellationToken cancellationToken)
    {
        Reads++;
        if (failure is not null)
        {
            return Task.FromException<OmpRelease>(failure);
        }

        return Task.FromResult(release ?? throw new InvalidOperationException("The fake has no release."));
    }
}

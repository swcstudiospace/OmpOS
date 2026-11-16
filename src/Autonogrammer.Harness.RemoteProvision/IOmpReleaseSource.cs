using Autonogrammer.Harness.OmpLauncher;

namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>Reads the omp release a remote machine should run: the latest one, as the registry names it.</summary>
public interface IOmpReleaseSource
{
    /// <summary>Reads the latest release.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The release with its version, tarball address and SHA-512.</returns>
    /// <exception cref="HttpRequestException">The registry could not be reached or answered with an error status.</exception>
    /// <exception cref="InvalidDataException">The registry's answer is not a usable package record.</exception>
    /// <exception cref="OperationCanceledException">The registry read timed out, or <paramref name="cancellationToken"/> was cancelled.</exception>
    Task<OmpRelease> ReadLatestAsync(CancellationToken cancellationToken);
}

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Runs one managed-install command. The seam keeps tests from spawning Bun or omp.
/// </summary>
public interface IOmpProcessRunner
{
    /// <summary>Runs the request and returns when it exits, times out or fails to start.</summary>
    /// <param name="request">What to run, where, and for how long.</param>
    /// <param name="cancellationToken">Cancels the wait. A cancelled run kills its process tree and throws.</param>
    /// <returns>The outcome of the run.</returns>
    Task<OmpProcessResult> RunAsync(OmpProcessRequest request, CancellationToken cancellationToken);
}

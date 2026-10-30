namespace Autonogrammer.Harness.Isolation;

/// <summary>
/// Starts one argument vector and returns its exit code and output. The probe uses it. Tests fake it so a probe test never starts a process.
/// </summary>
public interface IIsolationProcessRunner
{
    /// <summary>Runs <paramref name="fileName"/> with <paramref name="arguments"/> and stops waiting after <paramref name="timeout"/>.</summary>
    /// <param name="fileName">The executable.</param>
    /// <param name="arguments">The argument vector.</param>
    /// <param name="timeout">How long to wait for the process to exit.</param>
    /// <param name="cancellationToken">Cancels the wait. A timeout is reported as <see cref="IsolationProcessRun.TimedOut"/>, not as cancellation of the caller.</param>
    /// <returns>The exit code, combined output, and whether the executable was missing or the wait timed out.</returns>
    Task<IsolationProcessRun> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

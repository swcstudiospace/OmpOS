namespace OmpOS.Sandbox;

/// <summary>
/// Interface for running processes (injectable for testing).
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs a process asynchronously.
    /// </summary>
    /// <param name="spec">The process specification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process result.</returns>
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken = default);
}

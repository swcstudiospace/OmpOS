namespace Autonogrammer.Harness.Terminal;

/// <summary>
/// Runs one approved command. The gate calls this at most once per proposal.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Starts the process and waits for it to exit.
    /// </summary>
    /// <param name="request">The executable, argument list, working directory and output bound.</param>
    /// <param name="cancellationToken">Cancels the wait; the process is killed.</param>
    /// <returns>The exit code and captured output.</returns>
    Task<ProcessOutcome> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}

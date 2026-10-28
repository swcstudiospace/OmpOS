namespace Autonogrammer.Sandbox;

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

    /// <summary>
    /// Starts a process and yields each standard output line as soon as the process writes it.
    /// </summary>
    /// <remarks>
    /// The process tree is terminated when the sequence is disposed, when <see cref="ProcessSpec.Timeout"/>
    /// elapses (the sequence then ends without error), or when the caller cancels (the call then throws
    /// <see cref="OperationCanceledException"/>).
    /// Each yielded line is bounded: at most 64 KiB of a line is buffered and any excess up to the next
    /// line terminator (CR, LF or CRLF) is discarded, with a truncation marker appended so truncation is visible.
    /// </remarks>
    /// <param name="spec">The process specification.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The standard output lines, in the order the process wrote them.</returns>
    IAsyncEnumerable<string> StreamStdoutLinesAsync(ProcessSpec spec, CancellationToken cancellationToken = default);
}

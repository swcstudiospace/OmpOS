namespace OmpOS.Harness.Terminal;

/// <summary>
/// Starts interactive terminal sessions on the local machine. A session is started only from explicit options, and the
/// caller decides what is written to it; the approval gate in <see cref="TerminalGate"/> is not involved in interactive input.
/// </summary>
public interface ITerminalBackend
{
    /// <summary>
    /// Starts the process in a new terminal and returns once it has been spawned.
    /// </summary>
    /// <param name="options">The executable, arguments, working directory, size and output receiver.</param>
    /// <param name="cancellationToken">Cancels the spawn. It does not stop the session after it has started.</param>
    /// <returns>The running session. Dispose it to end the process if it is still running.</returns>
    Task<ITerminalSession> StartAsync(TerminalSessionOptions options, CancellationToken cancellationToken);
}

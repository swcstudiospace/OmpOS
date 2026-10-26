namespace OmpOS.Harness.Terminal;

/// <summary>
/// A running interactive terminal. Its output goes to the sink it was started with, as it arrives. Disposing the session
/// stops the process if it is still running, and waits for at most five seconds for the output to end. Disposal does not
/// bound a write the terminal has not completed: it waits for that write.
/// </summary>
public interface ITerminalSession : IAsyncDisposable
{
    /// <summary>
    /// Writes text to the process's input, encoded as UTF-8. Concurrent writes are applied one at a time, in call order.
    /// </summary>
    /// <remarks>
    /// The terminal echoes typed input, and that echo is part of the output, so input echoed by the terminal reaches the
    /// output sink. Anything typed while echo is on, including a secret, therefore reaches the sink.
    /// </remarks>
    /// <param name="text">The text to write, including any line ending the process expects.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="ObjectDisposedException">The session has been disposed, including while the write waits its turn.</exception>
    /// <exception cref="IOException">The write fails. This can happen once the process has exited, because it no longer reads its input.</exception>
    Task WriteInputAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the size of the terminal. A resize after the process has exited has no effect.
    /// </summary>
    /// <param name="columns">The number of columns. Must be at least 1.</param>
    /// <param name="rows">The number of rows. Must be at least 1.</param>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is less than 1.</exception>
    /// <exception cref="ObjectDisposedException">The session has been disposed.</exception>
    void Resize(int columns, int rows);

    /// <summary>
    /// Waits until the process has exited and its remaining output has reached the sink, then returns its exit code.
    /// Cancelling the wait does not stop the process. The output ends only when every process holding the terminal has
    /// closed it, so a background process that keeps the terminal open keeps this wait going until it exits or the token is cancelled.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The process exit code.</returns>
    /// <exception cref="ObjectDisposedException">The session has been disposed.</exception>
    Task<int> WaitForExitAsync(CancellationToken cancellationToken);
}

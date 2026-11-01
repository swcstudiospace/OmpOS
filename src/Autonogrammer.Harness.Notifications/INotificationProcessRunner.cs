namespace Autonogrammer.Harness.Notifications;

/// <summary>
/// Starts a notification process by argument vector. The executable and each argument are separate. This contract has no
/// shell-string parameter, so notification text cannot be spliced into a command line.
/// </summary>
public interface INotificationProcessRunner
{
    /// <summary>Runs <paramref name="fileName"/> with <paramref name="arguments"/> and optional standard input.</summary>
    /// <param name="fileName">The executable. Not a shell.</param>
    /// <param name="arguments">The argument vector. Notification text is an element, never joined into a command string.</param>
    /// <param name="standardInput">Bytes written to standard input, then the stream is closed. Empty when the command takes none.</param>
    /// <param name="cancellationToken">Cancels the wait and stops the process. The caller supplies the timeout through this token.</param>
    /// <returns>The exit code, or null when the executable is missing.</returns>
    Task<int?> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> standardInput,
        CancellationToken cancellationToken);
}

namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Runs one hook command. The hook runner calls this only after consent is recorded for the command, and at most once
/// per run. Tests substitute a fake to count spawns without starting any process.
/// </summary>
public interface IHookProcessRunner
{
    /// <summary>
    /// Starts the command and waits for it to exit. When <paramref name="cancellationToken"/> is cancelled, the process
    /// tree is killed and <see cref="OperationCanceledException"/> is thrown.
    /// </summary>
    /// <param name="command">The exact command to run.</param>
    /// <param name="maxOutputBytes">The most UTF-8 bytes kept for each of standard output and standard error.</param>
    /// <param name="cancellationToken">Cancels the wait and kills the process tree.</param>
    /// <returns>The exit code and the bounded output.</returns>
    Task<HookProcessOutcome> RunAsync(HookCommand command, int maxOutputBytes, CancellationToken cancellationToken);
}

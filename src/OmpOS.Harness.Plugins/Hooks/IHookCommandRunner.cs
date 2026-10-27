namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Runs one hook process with a payload on standard input. The dispatcher calls it only after consent and quarantine are
/// checked. Tests substitute a fake to count spawns without starting any process.
/// </summary>
public interface IHookCommandRunner
{
    /// <summary>
    /// Starts the process, writes the payload to its standard input and closes it, then waits for the exit. When
    /// <paramref name="cancellationToken"/> is cancelled the whole process tree is killed and
    /// <see cref="OperationCanceledException"/> is thrown. A process that cannot start is reported in the outcome, not thrown.
    /// </summary>
    /// <param name="invocation">What to run.</param>
    /// <param name="cancellationToken">Cancels the wait and kills the process tree.</param>
    /// <returns>The exit code and the bounded output.</returns>
    Task<HookCommandOutcome> RunAsync(HookInvocation invocation, CancellationToken cancellationToken);
}

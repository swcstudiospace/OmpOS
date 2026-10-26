using OmpOS.Harness.OmpLauncher;
using OmpOS.Harness.Terminal;

namespace OmpOS.Native.Views.Terminal;

/// <summary>
/// Starts omp in a terminal session for the omp terminal view. The shell supplies the implementation: it builds the
/// planner's <see cref="OmpTerminalLaunchRequest"/> (arguments, project directory, environment) and runs it through the
/// <see cref="OmpTerminalLauncher"/>. The view and its view model know nothing about the plan.
/// </summary>
public interface IOmpTerminalStarter
{
    /// <summary>
    /// Starts omp with a terminal of the given size. The view calls this again for every Restart.
    /// </summary>
    /// <param name="columns">The number of columns of the terminal. At least 1.</param>
    /// <param name="rows">The number of rows of the terminal. At least 1.</param>
    /// <param name="output">Receives the terminal output as it arrives, on a background thread. Pass it on to the request.</param>
    /// <param name="cancellationToken">Cancels the launch. A cancelled launch throws <see cref="OperationCanceledException"/>.</param>
    /// <returns>
    /// <see cref="OmpTerminalStarted"/> with the running session (the caller owns it and disposes it),
    /// <see cref="OmpTerminalNotFound"/> when omp is not installed, or <see cref="OmpTerminalLaunchFailed"/>. A missing omp is a
    /// result, not an exception.
    /// </returns>
    Task<OmpTerminalLaunchResult> StartAsync(int columns, int rows, IProcessOutputSink output, CancellationToken cancellationToken);
}

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The outcome of <see cref="OmpTerminalLauncher.LaunchAsync"/>. Consumers match on <see cref="OmpTerminalStarted"/>,
/// <see cref="OmpTerminalNotFound"/> or <see cref="OmpTerminalLaunchFailed"/>. A missing omp is a result, never an exception.
/// </summary>
public abstract class OmpTerminalLaunchResult
{
}

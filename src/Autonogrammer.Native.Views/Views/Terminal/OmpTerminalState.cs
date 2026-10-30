namespace Autonogrammer.Native.Views.Terminal;

/// <summary>
/// What the omp terminal is doing. The state changes only on the UI thread.
/// </summary>
internal enum OmpTerminalState
{
    /// <summary>No omp is running and none is starting: the terminal has not started yet, or it was stopped or closed.</summary>
    Idle,

    /// <summary>omp is being located and started.</summary>
    Starting,

    /// <summary>omp is running in the terminal.</summary>
    Running,

    /// <summary>omp ended. <see cref="OmpTerminalViewModel.ExitCode"/> holds its exit code, or null when the code is unknown.</summary>
    Exited,

    /// <summary>
    /// omp could not be started: it is not installed, or the session could not be opened. <see cref="OmpTerminalViewModel.FixText"/>
    /// says what to do.
    /// </summary>
    Unavailable,
}

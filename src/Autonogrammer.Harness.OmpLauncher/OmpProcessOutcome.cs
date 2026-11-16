namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>How a process run ended.</summary>
public enum OmpProcessOutcome
{
    /// <summary>The process exited on its own. Its exit code is set.</summary>
    Exited,

    /// <summary>The process exceeded its timeout and its process tree was killed.</summary>
    TimedOut,

    /// <summary>The process could not be started.</summary>
    StartFailed,
}

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Why a version probe failed.
/// </summary>
public enum OmpProbeFailure
{
    /// <summary>The executable could not be started.</summary>
    StartFailed,

    /// <summary>The probe exited with a non-zero code.</summary>
    NonZeroExit,

    /// <summary>The probe exceeded its timeout and its process tree was killed.</summary>
    TimedOut,

    /// <summary>The probe exited successfully but printed no version.</summary>
    EmptyOutput,
}

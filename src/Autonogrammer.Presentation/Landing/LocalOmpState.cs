namespace Autonogrammer.Presentation.Landing;

/// <summary>What the check of omp on this computer found.</summary>
public enum LocalOmpState
{
    /// <summary>The check has not run yet.</summary>
    Pending = 0,

    /// <summary>The check ran and found a runnable omp.</summary>
    Installed = 1,

    /// <summary>The check ran and found no runnable omp.</summary>
    NotInstalled = 2,

    /// <summary>The check threw.</summary>
    Failed = 3,

    /// <summary>No check is wired in this window.</summary>
    NotChecked = 4,
}

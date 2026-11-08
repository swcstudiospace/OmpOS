namespace Autonogrammer.Presentation.Landing;

/// <summary>What the check of omp on this computer found, or what an install of it is doing.</summary>
public enum LocalOmpState
{
    /// <summary>The check has not run yet.</summary>
    Pending = 0,

    /// <summary>The check ran and found a runnable omp, from the managed install.</summary>
    Installed = 1,

    /// <summary>The check ran and found no runnable omp.</summary>
    NotInstalled = 2,

    /// <summary>The check threw.</summary>
    Failed = 3,

    /// <summary>No check is wired in this window.</summary>
    NotChecked = 4,

    /// <summary>The check ran and found a runnable omp, but the managed install is missing, so the omp runs from the configured path or PATH.</summary>
    Missing = 5,

    /// <summary>A managed install of omp is running. The status carries its stage.</summary>
    Installing = 6,

    /// <summary>The managed install of omp did not complete. The status carries the reason.</summary>
    InstallFailed = 7,
}

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>Which source supplied the omp executable for a launch.</summary>
public enum OmpResolutionSource
{
    /// <summary>The explicitly configured absolute path.</summary>
    Configured,

    /// <summary>The managed active install, whose SHA-256 and version probe both passed.</summary>
    Managed,

    /// <summary>A PATH directory or a known install location.</summary>
    Path,

    /// <summary>No source supplied a runnable omp.</summary>
    None,
}

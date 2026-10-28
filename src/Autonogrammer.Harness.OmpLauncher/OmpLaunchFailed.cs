namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// The process did not start.
/// </summary>
public sealed class OmpLaunchFailed : OmpLaunchResult
{
    /// <summary>Creates a failed result.</summary>
    /// <param name="reason">Why the process did not start.</param>
    public OmpLaunchFailed(string reason)
    {
        Reason = reason;
    }

    /// <summary>Why the process did not start.</summary>
    public string Reason { get; }
}

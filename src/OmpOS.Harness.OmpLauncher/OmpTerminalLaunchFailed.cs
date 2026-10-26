namespace OmpOS.Harness.OmpLauncher;

/// <summary>
/// omp was found but the terminal session could not be started, for example because the project directory does not exist.
/// </summary>
public sealed class OmpTerminalLaunchFailed : OmpTerminalLaunchResult
{
    /// <summary>Creates a failed result.</summary>
    /// <param name="reason">A short explanation that is safe to show to the operator.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    public OmpTerminalLaunchFailed(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        Reason = reason;
    }

    /// <summary>A short explanation that is safe to show to the operator.</summary>
    public string Reason { get; }
}

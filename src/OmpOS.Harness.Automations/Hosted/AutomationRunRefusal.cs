namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Why the runner did not start a run.
/// </summary>
public enum AutomationRunRefusal
{
    /// <summary>The automation already has as many active runs as its concurrency limit allows.</summary>
    ConcurrencyLimit,

    /// <summary>The automation started as many runs in the last hour as its hourly cap allows.</summary>
    HourlyCap,

    /// <summary>The runner is shutting down.</summary>
    ShuttingDown,
}

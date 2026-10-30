namespace Autonogrammer.Harness.Automations;

/// <summary>
/// How one run attempt ended.
/// </summary>
public enum AutomationRunOutcome
{
    /// <summary>The body ran to completion.</summary>
    Succeeded,

    /// <summary>The body ran and faulted.</summary>
    Failed,

    /// <summary>The run exceeded its maximum runtime. Its body was cancelled.</summary>
    TimedOut,

    /// <summary>The caller cancelled the run. Its body was cancelled.</summary>
    Cancelled,

    /// <summary>A loop guard stopped the run before it started: a self-trigger or a chain deeper than the cap.</summary>
    Blocked,

    /// <summary>A guard refused the run before it started: disabled, paused, unconsented, rate limited or a failed guard store.</summary>
    Refused,
}

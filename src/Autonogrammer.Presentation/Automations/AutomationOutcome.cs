namespace Autonogrammer.Presentation.Automations;

/// <summary>How the latest run attempt of an automation ended, as an <see cref="IAutomationsSource"/> reports it.</summary>
public enum AutomationOutcome
{
    /// <summary>The body ran to completion.</summary>
    Succeeded,

    /// <summary>The body ran and faulted.</summary>
    Failed,

    /// <summary>The run exceeded its maximum runtime and was cancelled.</summary>
    TimedOut,

    /// <summary>The caller cancelled the run.</summary>
    Cancelled,

    /// <summary>A loop guard stopped the run before it started.</summary>
    Blocked,

    /// <summary>A guard refused the run before it started: disabled, paused, unconsented, rate limited or a failed guard store.</summary>
    Refused,
}

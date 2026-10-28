namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// How a run of an automation stands or ended.
/// </summary>
public enum AutomationRunStatus
{
    /// <summary>The run was admitted and its target has not finished.</summary>
    Running,

    /// <summary>The target finished the work successfully.</summary>
    Succeeded,

    /// <summary>The target accepted the work and continues on its own, for example a cloud agent that took the prompt. The run does not wait for it.</summary>
    Launched,

    /// <summary>The target reported a failure or threw.</summary>
    Failed,

    /// <summary>The run exceeded the automation's maximum runtime and was cancelled.</summary>
    TimedOut,

    /// <summary>The run was cancelled, for example because the host stopped.</summary>
    Cancelled,

    /// <summary>The scheduler skipped an occurrence because it was more than the misfire window late.</summary>
    Skipped,
}

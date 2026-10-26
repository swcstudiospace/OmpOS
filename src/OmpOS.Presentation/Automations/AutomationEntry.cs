namespace OmpOS.Presentation.Automations;

/// <summary>
/// One automation as an <see cref="IAutomationsSource"/> reports it. A head adapts the automation engine to this record, so the
/// presentation layer stays free of the harness (L2 to L3).
/// </summary>
/// <param name="Id">The automation identifier.</param>
/// <param name="Trigger">What starts the automation, in words such as <c>manual</c>, <c>schedule</c> or <c>event deploy.finished</c>.</param>
/// <param name="IsEnabled">Whether the automation is enabled. Automations are disabled until enabled explicitly.</param>
/// <param name="MaxRuntime">The longest a single run may last.</param>
/// <param name="LastRun">The latest run attempt, or null when the automation has not been attempted.</param>
/// <param name="Pause">The pause a guard placed on the automation, or null when it is not paused.</param>
public sealed record AutomationEntry(
    string Id,
    string Trigger,
    bool IsEnabled,
    TimeSpan MaxRuntime,
    AutomationLastRun? LastRun,
    AutomationPauseEntry? Pause);

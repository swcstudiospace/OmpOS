namespace Autonogrammer.Harness.Automations;

/// <summary>
/// The automation runs when its schedule is due, as evaluated against the engine's injected clock.
/// </summary>
public sealed record ScheduledTrigger : AutomationTrigger
{
    /// <summary>
    /// Creates a scheduled trigger.
    /// </summary>
    /// <param name="schedule">When the automation is due.</param>
    /// <exception cref="ArgumentNullException"><paramref name="schedule"/> is null.</exception>
    public ScheduledTrigger(AutomationSchedule schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        Schedule = schedule;
    }

    /// <summary>When the automation is due.</summary>
    public AutomationSchedule Schedule { get; }
}

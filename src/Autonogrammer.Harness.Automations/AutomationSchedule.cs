namespace Autonogrammer.Harness.Automations;

/// <summary>
/// When a scheduled automation is due: first at <see cref="FirstRunAt"/>, then once every <see cref="Interval"/>.
/// Occurrences missed while the engine was not asked to run coalesce into a single run.
/// </summary>
public sealed record AutomationSchedule
{
    /// <summary>
    /// Creates a schedule.
    /// </summary>
    /// <param name="firstRunAt">The first instant the automation is due.</param>
    /// <param name="interval">The time between due instants. Must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">The interval is not positive.</exception>
    public AutomationSchedule(DateTimeOffset firstRunAt, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "The interval must be positive.");
        }

        FirstRunAt = firstRunAt;
        Interval = interval;
    }

    /// <summary>The first instant the automation is due.</summary>
    public DateTimeOffset FirstRunAt { get; }

    /// <summary>The time between due instants.</summary>
    public TimeSpan Interval { get; }
}

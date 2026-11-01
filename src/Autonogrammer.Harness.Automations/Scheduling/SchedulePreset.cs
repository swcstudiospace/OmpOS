#pragma warning disable MA0048 // The plan owns this file for the preset and its cron map.

namespace Autonogrammer.Harness.Automations.Scheduling;

/// <summary>
/// The editor's schedule choices. Each one becomes a five-field cron expression; the stored trigger stays cron.
/// </summary>
public enum SchedulePreset
{
    /// <summary>Every hour, on the hour.</summary>
    Hourly,

    /// <summary>Every day at 09:00 in the automation's zone.</summary>
    Daily,

    /// <summary>Every Monday at 09:00 in the automation's zone.</summary>
    Weekly,

    /// <summary>Monday to Friday at 09:00 in the automation's zone.</summary>
    Weekdays,

    /// <summary>No recurring run. The mapped expression never matches a calendar day.</summary>
    Manual,
}

/// <summary>Turns a <see cref="SchedulePreset"/> into the five-field cron the scheduler already understands.</summary>
public static class SchedulePresetMap
{
    /// <summary>Maps a preset to a five-field cron expression.</summary>
    /// <param name="preset">The preset.</param>
    /// <returns>The expression. Weekdays is Monday to Friday (<c>1-5</c>).</returns>
    public static string ToCron(SchedulePreset preset) => preset switch
    {
        SchedulePreset.Hourly => "0 * * * *",
        SchedulePreset.Daily => "0 9 * * *",
        SchedulePreset.Weekly => "0 9 * * 1",
        SchedulePreset.Weekdays => "0 9 * * 1-5",
        SchedulePreset.Manual => "0 0 31 2 *",
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "The schedule preset is not known."),
    };
}

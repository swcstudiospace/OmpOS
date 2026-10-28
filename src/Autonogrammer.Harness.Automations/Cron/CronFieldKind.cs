namespace Autonogrammer.Harness.Automations.Cron;

/// <summary>
/// The five fields of a cron expression, in order. The kind decides the allowed range and the names a field accepts.
/// </summary>
internal enum CronFieldKind
{
    /// <summary>Minute of the hour, 0 to 59.</summary>
    Minute,

    /// <summary>Hour of the day, 0 to 23.</summary>
    Hour,

    /// <summary>Day of the month, 1 to 31.</summary>
    DayOfMonth,

    /// <summary>Month of the year, 1 to 12 or <c>jan</c> to <c>dec</c>.</summary>
    Month,

    /// <summary>Day of the week, 0 to 7 (0 and 7 are Sunday) or <c>sun</c> to <c>sat</c>.</summary>
    DayOfWeek,
}

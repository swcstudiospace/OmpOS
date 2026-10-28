namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Time zones built in code, so the clock-change tests do not depend on the time zone database of the machine that runs them.
/// </summary>
internal static class TestZones
{
    /// <summary>
    /// UTC-5 in winter and UTC-4 in summer: forward at 02:00 on the second Sunday of March (2026-03-08, 07:00 UTC), back at 02:00 on the
    /// first Sunday of November (2026-11-01, 06:00 UTC).
    /// </summary>
    public static readonly TimeZoneInfo NewYorkLike = Build(
        "Test/NewYorkLike",
        TimeSpan.FromHours(-5),
        TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified), 3, 2, DayOfWeek.Sunday),
        TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified), 11, 1, DayOfWeek.Sunday));

    /// <summary>
    /// UTC+1 in winter and UTC+2 in summer: forward at 02:00 on the last Sunday of March (2026-03-29, 01:00 UTC), back at 03:00 on the last
    /// Sunday of October (2026-10-25, 01:00 UTC).
    /// </summary>
    public static readonly TimeZoneInfo BerlinLike = Build(
        "Test/BerlinLike",
        TimeSpan.FromHours(1),
        TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified), 3, 5, DayOfWeek.Sunday),
        TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0, DateTimeKind.Unspecified), 10, 5, DayOfWeek.Sunday));

    private static TimeZoneInfo Build(string id, TimeSpan baseOffset, TimeZoneInfo.TransitionTime start, TimeZoneInfo.TransitionTime end)
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue.Date, DateTime.MaxValue.Date, TimeSpan.FromHours(1), start, end);
        return TimeZoneInfo.CreateCustomTimeZone(id, baseOffset, id, id + " standard", id + " daylight", [rule]);
    }
}

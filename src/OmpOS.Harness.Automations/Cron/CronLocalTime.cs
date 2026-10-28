namespace OmpOS.Harness.Automations.Cron;

/// <summary>
/// Maps a local wall-clock time in a time zone to the instants it denotes, applying the clock-change rules of
/// <see cref="CronExpression"/>: a time that does not exist (the hour skipped when clocks go forward) maps to the instant the
/// clock changes, and a time that happens twice (the hour repeated when clocks go back) maps to its first occurrence, plus its
/// second occurrence when the caller asks for the repeat.
/// </summary>
internal static class CronLocalTime
{
    private static readonly TimeSpan GapSearchWindow = TimeSpan.FromHours(26);

    /// <summary>Maps a local time to one or two UTC instants.</summary>
    /// <param name="local">The wall-clock time, of kind unspecified.</param>
    /// <param name="zone">The time zone the wall clock belongs to.</param>
    /// <param name="wildcardHour">True when the schedule's hour field is a wildcard (<c>*</c> or <c>*/n</c>).</param>
    /// <param name="first">The first (or only) instant, in UTC.</param>
    /// <param name="second">The second instant, in UTC, when the method returns 2.</param>
    /// <returns>The number of instants: 0, 1 or 2.</returns>
    public static int Map(DateTime local, TimeZoneInfo zone, bool wildcardHour, out DateTimeOffset first, out DateTimeOffset second)
    {
        ArgumentNullException.ThrowIfNull(zone);
        second = default;
        if (zone.IsInvalidTime(local))
        {
            first = ClockChangeInstant(local, zone);
            return wildcardHour ? 0 : 1;
        }

        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            var earliest = offsets[0];
            var latest = offsets[0];
            foreach (var offset in offsets)
            {
                earliest = offset < earliest ? offset : earliest;
                latest = offset > latest ? offset : latest;
            }

            // The larger offset is the earlier instant: the clock has not been put back yet.
            first = new DateTimeOffset(local, latest).ToUniversalTime();
            if (!wildcardHour || earliest == latest)
            {
                return 1;
            }

            second = new DateTimeOffset(local, earliest).ToUniversalTime();
            return 2;
        }

        first = new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
        return 1;
    }

    /// <summary>Returns the instant the zone's offset changes for a local time that falls in the skipped interval.</summary>
    /// <param name="local">A wall-clock time that does not exist in the zone.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns>The first minute that has the offset after the change, in UTC.</returns>
    private static DateTimeOffset ClockChangeInstant(DateTime local, TimeZoneInfo zone)
    {
        var before = zone.GetUtcOffset(local.AddDays(-2));
        var upper = new DateTimeOffset(local, before).ToUniversalTime();
        var lower = upper - GapSearchWindow;
        while (upper - lower > TimeSpan.FromMinutes(1))
        {
            var middle = lower + TimeSpan.FromTicks((upper - lower).Ticks / 2);
            middle = new DateTimeOffset(middle.Ticks - (middle.Ticks % TimeSpan.TicksPerMinute), TimeSpan.Zero);
            if (middle <= lower)
            {
                break;
            }

            if (zone.GetUtcOffset(middle) == before)
            {
                lower = middle;
            }
            else
            {
                upper = middle;
            }
        }

        return upper;
    }
}

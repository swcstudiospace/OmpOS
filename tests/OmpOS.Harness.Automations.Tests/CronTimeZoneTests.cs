using System.Globalization;
using OmpOS.Harness.Automations.Cron;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies the clock-change rules: a skipped local time fires once at the instant the clock changes, a repeated local time fires once
/// at its first occurrence unless the hour field is a wildcard, and ordinary days follow the zone's offset.
/// </summary>
public sealed class CronTimeZoneTests
{
    private static DateTimeOffset Utc(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    [Fact]
    public void The_zones_behave_as_documented()
    {
        // The expected instants of every test below rest on these four offsets.
        TestZones.NewYorkLike.GetUtcOffset(Utc("2026-03-08T06:59:00Z")).ShouldBe(TimeSpan.FromHours(-5));
        TestZones.NewYorkLike.GetUtcOffset(Utc("2026-03-08T07:00:00Z")).ShouldBe(TimeSpan.FromHours(-4));
        TestZones.NewYorkLike.GetUtcOffset(Utc("2026-11-01T05:59:00Z")).ShouldBe(TimeSpan.FromHours(-4));
        TestZones.NewYorkLike.GetUtcOffset(Utc("2026-11-01T06:00:00Z")).ShouldBe(TimeSpan.FromHours(-5));
    }

    [Fact]
    public void A_local_time_in_the_skipped_hour_fires_at_the_instant_the_clock_changes()
    {
        var next = CronExpression.Parse("30 2 * * *").GetNextOccurrence(Utc("2026-03-07T08:00:00Z"), TestZones.NewYorkLike);

        next.ShouldBe(Utc("2026-03-08T07:00:00Z"));
    }

    [Fact]
    public void The_day_after_the_clock_change_runs_at_the_new_offset()
    {
        var occurrences = CronExpression.Parse("30 2 * * *").GetNextOccurrences(Utc("2026-03-07T08:00:00Z"), TestZones.NewYorkLike, 3);

        occurrences.ShouldBe([Utc("2026-03-08T07:00:00Z"), Utc("2026-03-09T06:30:00Z"), Utc("2026-03-10T06:30:00Z")]);
    }

    [Fact]
    public void Several_skipped_local_times_collapse_into_one_occurrence()
    {
        var occurrences = CronExpression.Parse("*/30 2 * * *").GetNextOccurrences(Utc("2026-03-08T06:00:00Z"), TestZones.NewYorkLike, 3);

        occurrences.ShouldBe([Utc("2026-03-08T07:00:00Z"), Utc("2026-03-09T06:00:00Z"), Utc("2026-03-09T06:30:00Z")]);
    }

    [Fact]
    public void A_time_just_after_the_gap_is_not_disturbed()
    {
        var next = CronExpression.Parse("0 3 * * *").GetNextOccurrence(Utc("2026-03-07T12:00:00Z"), TestZones.NewYorkLike);

        next.ShouldBe(Utc("2026-03-08T07:00:00Z"));
    }

    [Fact]
    public void A_repeated_local_time_fires_once_at_its_first_occurrence()
    {
        var occurrences = CronExpression.Parse("30 1 * * *").GetNextOccurrences(Utc("2026-10-31T12:00:00Z"), TestZones.NewYorkLike, 2);

        occurrences.ShouldBe([Utc("2026-11-01T05:30:00Z"), Utc("2026-11-02T06:30:00Z")]);
    }

    [Fact]
    public void A_repeated_local_time_does_not_fire_again_in_its_second_pass()
    {
        var next = CronExpression.Parse("30 1 * * *").GetNextOccurrence(Utc("2026-11-01T05:45:00Z"), TestZones.NewYorkLike);

        next.ShouldBe(Utc("2026-11-02T06:30:00Z"));
    }

    [Fact]
    public void An_hourly_schedule_keeps_its_rhythm_through_the_repeated_hour()
    {
        var occurrences = CronExpression.Parse("30 * * * *").GetNextOccurrences(Utc("2026-11-01T04:00:00Z"), TestZones.NewYorkLike, 5);

        occurrences.ShouldBe(
        [
            Utc("2026-11-01T04:30:00Z"),
            Utc("2026-11-01T05:30:00Z"),
            Utc("2026-11-01T06:30:00Z"),
            Utc("2026-11-01T07:30:00Z"),
            Utc("2026-11-01T08:30:00Z"),
        ]);
    }

    [Fact]
    public void A_stepped_hour_schedule_also_repeats_in_the_repeated_hour()
    {
        var occurrences = CronExpression.Parse("15 */1 * * *").GetNextOccurrences(Utc("2026-11-01T05:00:00Z"), TestZones.NewYorkLike, 3);

        occurrences.ShouldBe([Utc("2026-11-01T05:15:00Z"), Utc("2026-11-01T06:15:00Z"), Utc("2026-11-01T07:15:00Z")]);
    }

    [Fact]
    public void An_hourly_schedule_skips_the_nonexistent_local_time()
    {
        var occurrences = CronExpression.Parse("30 * * * *").GetNextOccurrences(Utc("2026-03-08T05:00:00Z"), TestZones.NewYorkLike, 4);

        // 00:30 EST, 01:30 EST, then 03:30 EDT: the 02:30 local time does not exist and an hourly schedule skips it.
        occurrences.ShouldBe(
        [
            Utc("2026-03-08T05:30:00Z"),
            Utc("2026-03-08T06:30:00Z"),
            Utc("2026-03-08T07:30:00Z"),
            Utc("2026-03-08T08:30:00Z"),
        ]);
    }

    [Fact]
    public void An_european_zone_fires_a_skipped_time_at_the_change_and_a_repeated_time_once()
    {
        var spring = CronExpression.Parse("30 2 * * *").GetNextOccurrence(Utc("2026-03-28T12:00:00Z"), TestZones.BerlinLike);
        var autumn = CronExpression.Parse("30 2 * * *").GetNextOccurrence(Utc("2026-10-24T12:00:00Z"), TestZones.BerlinLike);

        spring.ShouldBe(Utc("2026-03-29T01:00:00Z"));
        autumn.ShouldBe(Utc("2026-10-25T00:30:00Z"));
    }

    [Fact]
    public void A_late_evening_time_lands_on_the_next_utc_day()
    {
        var next = CronExpression.Parse("0 23 * * *").GetNextOccurrence(Utc("2026-01-01T03:00:00Z"), TestZones.NewYorkLike);

        next.ShouldBe(Utc("2026-01-01T04:00:00Z"));
    }

    [Fact]
    public void A_leap_day_in_a_zone_lands_at_the_zone_midnight()
    {
        var next = CronExpression.Parse("0 0 29 2 *").GetNextOccurrence(Utc("2027-01-01T00:00:00Z"), TestZones.NewYorkLike);

        next.ShouldBe(Utc("2028-02-29T05:00:00Z"));
    }

    [Fact]
    public void A_month_end_in_a_zone_uses_the_zone_calendar()
    {
        // 2026-03-31 23:30 local in the summer-time zone is 2026-04-01 03:30 UTC.
        var next = CronExpression.Parse("30 23 31 * *").GetNextOccurrence(Utc("2026-03-31T00:00:00Z"), TestZones.NewYorkLike);

        next.ShouldBe(Utc("2026-04-01T03:30:00Z"));
    }

    [Theory]
    [InlineData("2026-03-06T00:00:00Z")]
    [InlineData("2026-10-30T00:00:00Z")]
    public void Every_occurrence_is_strictly_increasing_across_a_clock_change(string start)
    {
        var occurrences = CronExpression.Parse("*/20 0-4 * * *").GetNextOccurrences(Utc(start), TestZones.NewYorkLike, 90);

        occurrences.Count.ShouldBe(90);
        for (var index = 1; index < occurrences.Count; index++)
        {
            occurrences[index].ShouldBeGreaterThan(occurrences[index - 1]);
        }
    }

    [Fact]
    public void Three_skipped_times_of_a_fixed_hour_fire_once_at_the_change()
    {
        var occurrences = CronExpression.Parse("*/20 2 * * *").GetNextOccurrences(Utc("2026-03-08T00:00:00Z"), TestZones.NewYorkLike, 2);

        occurrences.ShouldBe([Utc("2026-03-08T07:00:00Z"), Utc("2026-03-09T06:00:00Z")]);
    }
}

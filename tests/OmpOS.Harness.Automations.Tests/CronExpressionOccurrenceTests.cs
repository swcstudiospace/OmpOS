using System.Globalization;
using OmpOS.Harness.Automations.Cron;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>Verifies the next-occurrence calculation in UTC: field arithmetic, month ends, leap days and the day-of-month or day-of-week rule.</summary>
public sealed class CronExpressionOccurrenceTests
{
    private static DateTimeOffset Utc(string text) => DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    [Theory]
    [InlineData("* * * * *", "2026-01-01T00:00:00Z", "2026-01-01T00:01:00Z")]
    [InlineData("* * * * *", "2026-01-01T00:00:30Z", "2026-01-01T00:01:00Z")]
    [InlineData("* * * * *", "2026-01-01T00:59:59Z", "2026-01-01T01:00:00Z")]
    [InlineData("*/15 * * * *", "2026-01-01T10:07:00Z", "2026-01-01T10:15:00Z")]
    [InlineData("*/15 * * * *", "2026-01-01T10:44:59Z", "2026-01-01T10:45:00Z")]
    [InlineData("*/15 * * * *", "2026-01-01T10:45:00Z", "2026-01-01T11:00:00Z")]
    [InlineData("0 9 * * *", "2026-03-05T08:59:00Z", "2026-03-05T09:00:00Z")]
    [InlineData("0 9 * * *", "2026-03-05T09:00:00Z", "2026-03-06T09:00:00Z")]
    [InlineData("0 9 * * mon-fri", "2026-01-02T09:00:00Z", "2026-01-05T09:00:00Z")]
    [InlineData("0 9 * * MON-FRI", "2026-01-03T12:00:00Z", "2026-01-05T09:00:00Z")]
    [InlineData("0 0 * * 0", "2026-01-01T00:00:00Z", "2026-01-04T00:00:00Z")]
    [InlineData("0 0 * * 7", "2026-01-01T00:00:00Z", "2026-01-04T00:00:00Z")]
    [InlineData("0 0 1 * *", "2026-01-31T00:00:00Z", "2026-02-01T00:00:00Z")]
    [InlineData("0 0 1 * *", "2026-12-31T23:59:00Z", "2027-01-01T00:00:00Z")]
    [InlineData("0 0 31 * *", "2026-01-31T00:00:00Z", "2026-03-31T00:00:00Z")]
    [InlineData("0 0 31 * *", "2026-03-31T00:00:00Z", "2026-05-31T00:00:00Z")]
    [InlineData("0 0 */2 * *", "2026-01-31T00:00:00Z", "2026-02-01T00:00:00Z")]
    [InlineData("0 0 * jan,mar *", "2026-01-31T00:00:00Z", "2026-03-01T00:00:00Z")]
    [InlineData("0-30/10 * * * *", "2026-01-01T10:20:00Z", "2026-01-01T10:30:00Z")]
    [InlineData("0-30/10 * * * *", "2026-01-01T10:30:00Z", "2026-01-01T11:00:00Z")]
    [InlineData("5/20 * * * *", "2026-01-01T10:00:00Z", "2026-01-01T10:05:00Z")]
    [InlineData("5/20 * * * *", "2026-01-01T10:45:00Z", "2026-01-01T11:05:00Z")]
    [InlineData("0 0 29 2 *", "2026-01-01T00:00:00Z", "2028-02-29T00:00:00Z")]
    [InlineData("0 0 29 2 *", "2028-02-29T00:00:00Z", "2032-02-29T00:00:00Z")]
    [InlineData("0 0 29 2 *", "2096-03-01T00:00:00Z", "2104-02-29T00:00:00Z")]
    [InlineData("@hourly", "2026-06-15T13:20:00Z", "2026-06-15T14:00:00Z")]
    [InlineData("@daily", "2026-06-15T13:20:00Z", "2026-06-16T00:00:00Z")]
    [InlineData("@weekly", "2026-06-15T13:20:00Z", "2026-06-21T00:00:00Z")]
    [InlineData("@monthly", "2026-06-15T13:20:00Z", "2026-07-01T00:00:00Z")]
    [InlineData("@yearly", "2026-06-15T13:20:00Z", "2027-01-01T00:00:00Z")]
    [InlineData("59 23 31 12 *", "2026-12-31T23:58:59Z", "2026-12-31T23:59:00Z")]
    [InlineData("59 23 31 12 *", "2026-12-31T23:59:00Z", "2027-12-31T23:59:00Z")]
    public void The_next_occurrence_after_an_instant(string expression, string after, string expected)
    {
        var next = CronExpression.Parse(expression).GetNextOccurrence(Utc(after), TimeZoneInfo.Utc);

        next.ShouldBe(Utc(expected));
    }

    [Fact]
    public void An_occurrence_is_always_strictly_after_the_instant()
    {
        var cron = CronExpression.Parse("*/5 * * * *");
        var at = Utc("2026-01-01T00:00:00Z");

        for (var step = 0; step < 50; step++)
        {
            var next = cron.GetNextOccurrence(at, TimeZoneInfo.Utc);

            next.ShouldNotBeNull();
            next.Value.ShouldBeGreaterThan(at);
            at = next.Value;
        }
    }

    [Fact]
    public void A_date_that_never_exists_has_no_occurrence()
    {
        CronExpression.Parse("0 0 30 2 *").GetNextOccurrence(Utc("2026-01-01T00:00:00Z"), TimeZoneInfo.Utc).ShouldBeNull();
        CronExpression.Parse("0 0 31 4 *").GetNextOccurrence(Utc("2026-01-01T00:00:00Z"), TimeZoneInfo.Utc).ShouldBeNull();
    }

    [Fact]
    public void When_both_day_fields_are_restricted_either_one_matches()
    {
        var occurrences = CronExpression.Parse("0 0 13 * fri").GetNextOccurrences(Utc("2026-01-01T00:00:00Z"), TimeZoneInfo.Utc, 7);

        occurrences.ShouldBe(
        [
            Utc("2026-01-02T00:00:00Z"),
            Utc("2026-01-09T00:00:00Z"),
            Utc("2026-01-13T00:00:00Z"),
            Utc("2026-01-16T00:00:00Z"),
            Utc("2026-01-23T00:00:00Z"),
            Utc("2026-01-30T00:00:00Z"),
            Utc("2026-02-06T00:00:00Z"),
        ]);
    }

    [Fact]
    public void When_the_week_day_field_is_a_wildcard_the_day_of_month_alone_decides()
    {
        var occurrences = CronExpression.Parse("0 0 13 * *").GetNextOccurrences(Utc("2026-01-01T00:00:00Z"), TimeZoneInfo.Utc, 3);

        occurrences.ShouldBe([Utc("2026-01-13T00:00:00Z"), Utc("2026-02-13T00:00:00Z"), Utc("2026-03-13T00:00:00Z")]);
    }

    [Fact]
    public void GetNextOccurrences_stops_early_when_the_expression_runs_out()
    {
        var occurrences = CronExpression.Parse("0 0 30 2 *").GetNextOccurrences(Utc("2026-01-01T00:00:00Z"), TimeZoneInfo.Utc, 5);

        occurrences.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(CronExpression.MaxOccurrences + 1)]
    public void GetNextOccurrences_rejects_a_count_outside_its_range(int count)
    {
        var cron = CronExpression.Parse("* * * * *");

        Should.Throw<ArgumentOutOfRangeException>(() => cron.GetNextOccurrences(Utc("2026-01-01T00:00:00Z"), TimeZoneInfo.Utc, count));
    }

    [Fact]
    public void The_largest_count_is_served()
    {
        var occurrences = CronExpression.Parse("* * * * *").GetNextOccurrences(Utc("2026-01-01T00:00:00Z"), TimeZoneInfo.Utc, CronExpression.MaxOccurrences);

        occurrences.Count.ShouldBe(CronExpression.MaxOccurrences);
        occurrences[^1].ShouldBe(Utc("2026-01-01T00:00:00Z").AddMinutes(CronExpression.MaxOccurrences));
    }

    [Fact]
    public void An_offset_input_is_compared_as_the_same_instant()
    {
        var after = new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.FromHours(2));

        var next = CronExpression.Parse("0 11 * * *").GetNextOccurrence(after, TimeZoneInfo.Utc);

        next.ShouldBe(Utc("2026-01-02T11:00:00Z"));
    }
}

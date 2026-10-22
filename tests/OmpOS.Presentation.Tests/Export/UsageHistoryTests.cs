using OmpOS.Presentation.Export;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// Verifies usage history (ADE-20): runs list in stable order, and totals per day and per month are taken in UTC.
/// </summary>
public sealed class UsageHistoryTests
{
    /// <summary>ADE-20: the runs list in timestamp order, then by run id, whatever order they were supplied in.</summary>
    [Fact]
    public void Runs_AreOrderedByTimestampThenRunId()
    {
        var history = new UsageHistory(FixtureRuns().Reverse());

        history.Runs.Select(static run => run.RunId).ShouldBe(["r-1", "r-2", "r-3", "r-5", "r-4"]);
    }

    /// <summary>ADE-20: runs with the same timestamp are listed by run id, so the listing never depends on input order.</summary>
    [Fact]
    public void Runs_SameTimestamp_OrderByRunIdOrdinal()
    {
        var at = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
        var history = new UsageHistory(
        [
            new UsageRecord("run-b", at, "model-a", 1, 1),
            new UsageRecord("run-A", at, "model-a", 1, 1),
            new UsageRecord("run-a", at, "model-a", 1, 1),
        ]);

        history.Runs.Select(static run => run.RunId).ShouldBe(["run-A", "run-a", "run-b"]);
    }

    /// <summary>ADE-20: a timestamp with an offset is kept as the same instant in UTC.</summary>
    [Fact]
    public void Record_TimestampWithOffset_IsStoredInUtc()
    {
        var record = new UsageRecord("r-2", new DateTimeOffset(2026, 10, 20, 18, 30, 0, TimeSpan.FromHours(2)), "model-b", 50, 10);

        record.TimestampUtc.ShouldBe(new DateTimeOffset(2026, 10, 20, 16, 30, 0, TimeSpan.Zero));
        record.TimestampUtc.Offset.ShouldBe(TimeSpan.Zero);
    }

    /// <summary>ADE-20: totals per day sum the runs of each UTC day, in chronological order.</summary>
    [Fact]
    public void Totals_PerDay_SumRunsOfEachUtcDay()
    {
        var totals = new UsageHistory(FixtureRuns()).Totals(UsagePeriod.Day);

        totals.ShouldBe(
        [
            new UsageTotal("2026-10-20", 2, 150, 50),
            new UsageTotal("2026-10-21", 1, 300, 120),
            new UsageTotal("2026-10-22", 1, 7, 3),
            new UsageTotal("2026-11-02", 1, 10, 5),
        ]);
    }

    /// <summary>ADE-20: totals per month sum the runs of each UTC month, in chronological order.</summary>
    [Fact]
    public void Totals_PerMonth_SumRunsOfEachUtcMonth()
    {
        var totals = new UsageHistory(FixtureRuns()).Totals(UsagePeriod.Month);

        totals.ShouldBe(
        [
            new UsageTotal("2026-10", 4, 457, 173),
            new UsageTotal("2026-11", 1, 10, 5),
        ]);
    }

    /// <summary>ADE-20: a total's token count is its input and output tokens together.</summary>
    [Fact]
    public void Totals_TotalTokens_AddsInputAndOutput()
    {
        var october = new UsageHistory(FixtureRuns()).Totals(UsagePeriod.Month)[0];

        october.TotalTokens.ShouldBe(630);
    }

    /// <summary>ADE-20: totals do not depend on the order runs were supplied in.</summary>
    [Fact]
    public void Totals_AreTheSameForAnyInputOrder()
    {
        var forward = new UsageHistory(FixtureRuns()).Totals(UsagePeriod.Day);
        var backward = new UsageHistory(FixtureRuns().Reverse()).Totals(UsagePeriod.Day);

        backward.ShouldBe(forward);
    }

    /// <summary>ADE-20: an empty history has no runs and no totals.</summary>
    [Fact]
    public void Empty_HasNoRunsAndNoTotals()
    {
        var history = new UsageHistory([]);

        history.Runs.ShouldBeEmpty();
        history.Totals(UsagePeriod.Month).ShouldBeEmpty();
    }

    /// <summary>ADE-20: a period outside the defined values is rejected.</summary>
    [Fact]
    public void Totals_UnknownPeriod_Throws()
    {
        var history = new UsageHistory(FixtureRuns());

        Should.Throw<ArgumentOutOfRangeException>(() => history.Totals((UsagePeriod)99));
    }

    /// <summary>ADE-20: a run id must not be blank.</summary>
    [Fact]
    public void Record_BlankRunId_Throws()
    {
        Should.Throw<ArgumentException>(() => new UsageRecord(" ", DateTimeOffset.UnixEpoch, "model-a", 1, 1));
    }

    /// <summary>ADE-20: a model must not be blank.</summary>
    [Fact]
    public void Record_BlankModel_Throws()
    {
        Should.Throw<ArgumentException>(() => new UsageRecord("r-1", DateTimeOffset.UnixEpoch, "", 1, 1));
    }

    /// <summary>ADE-20: token counts are never negative.</summary>
    [Fact]
    public void Record_NegativeTokens_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new UsageRecord("r-1", DateTimeOffset.UnixEpoch, "model-a", -1, 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new UsageRecord("r-1", DateTimeOffset.UnixEpoch, "model-a", 0, -1));
    }

    /// <summary>ADE-20: a null record in the runs is rejected.</summary>
    [Fact]
    public void Constructor_NullRecord_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new UsageHistory([null!]));
    }

    private static IReadOnlyList<UsageRecord> FixtureRuns() =>
    [
        new UsageRecord("r-1", new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero), "model-a", 100, 40),
        new UsageRecord("r-2", new DateTimeOffset(2026, 10, 20, 18, 30, 0, TimeSpan.FromHours(2)), "model-b", 50, 10),
        new UsageRecord("r-3", new DateTimeOffset(2026, 10, 21, 0, 15, 0, TimeSpan.Zero), "model-a", 300, 120),
        new UsageRecord("r-4", new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero), "model-b", 10, 5),
        new UsageRecord("r-5", new DateTimeOffset(2026, 10, 21, 23, 59, 59, TimeSpan.FromHours(-5)), "model-a", 7, 3),
    ];
}

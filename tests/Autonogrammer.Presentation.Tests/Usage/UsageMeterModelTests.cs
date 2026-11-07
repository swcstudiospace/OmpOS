using Autonogrammer.Presentation.Usage;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Usage;

/// <summary>Per-session and per-day usage, with occupancy never summed and a missing cost left unavailable.</summary>
public sealed class UsageMeterModelTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    /// <summary>Last and peak occupancy, last session cost, and a day's cost as the sum of its sessions.</summary>
    [Fact]
    public void SessionsKeepLastAndPeakAndDaysSumSessionCosts()
    {
        var meter = UsageMeterModel.Create(
        [
            new UsageMeterModel.Sample("s1", Day, 1000, 100, 1.00m, "USD"),
            new UsageMeterModel.Sample("s1", Day, 1000, 250, 1.50m, "USD"),
            new UsageMeterModel.Sample("s2", Day, 800, 40, 0.25m, "USD"),
            new UsageMeterModel.Sample("s1", Day.AddDays(1), 1000, 80, 2.00m, "USD"),
        ]);

        var first = meter.Sessions.Single(row => string.Equals(row.SessionId, "s1", StringComparison.Ordinal));
        first.LastUsed.ShouldBe(80);
        first.PeakUsed.ShouldBe(250);
        first.Amount.ShouldBe(2.00m);
        first.Size.ShouldBe(1000);
        meter.Sessions.Single(row => string.Equals(row.SessionId, "s2", StringComparison.Ordinal)).Amount.ShouldBe(0.25m);

        var opening = meter.Days[0];
        opening.Day.ShouldBe(Day);
        opening.SessionCount.ShouldBe(2);
        opening.PeakUsed.ShouldBe(250);
        opening.LastUsed.ShouldBe(40);
        opening.Amount.ShouldBe(1.75m);
        opening.Unavailable.ShouldBeFalse();
        meter.Days[1].Amount.ShouldBe(2.00m);
        meter.Days[1].PeakUsed.ShouldBe(80);
        (first.LastUsed + meter.Sessions[1].LastUsed).ShouldNotBe(100 + 250 + 40 + 80);
    }

    /// <summary>A report with no cost shows UsageUnavailable instead of zero.</summary>
    [Fact]
    public void AReportWithoutCostIsUnavailableInsteadOfZero()
    {
        var meter = UsageMeterModel.Create([new UsageMeterModel.Sample("s1", Day, 1000, 10, null, null)]);

        meter.CostUnavailable.ShouldBeTrue();
        meter.UnavailableText.ShouldBe(UsageMeterModel.UsageUnavailable);
        meter.Sessions[0].Unavailable.ShouldBeTrue();
        meter.Sessions[0].Amount.ShouldBeNull();
        meter.Days[0].Unavailable.ShouldBeTrue();
        meter.Days[0].Amount.ShouldNotBe(0m);
        meter.ToString().ShouldBe(nameof(UsageMeterModel));
        meter.Days[0].ToString().ShouldNotContain("10");
    }
}

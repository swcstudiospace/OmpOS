using Shouldly;
using Xunit;

namespace OmpOS.Harness.Herdr.Tests;

public sealed class HerdrHeartbeatMonitorTests
{
    private static readonly DateTimeOffset Start = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly HerdrSessionId Alpha = new("alpha");
    private static readonly TimeSpan JustBeforeStale = TimeSpan.FromMilliseconds(44_900);
    private static readonly TimeSpan Hundredth = TimeSpan.FromMilliseconds(100);

    [Fact]
    public void StaleIsReportedAtFortyFiveSecondsAndNotAtFortyFourPointNineSeconds()
    {
        var clock = new ManualTimeProvider(Start);
        var monitor = new HerdrHeartbeatMonitor(clock);
        monitor.Track(Alpha);

        clock.Advance(JustBeforeStale);
        monitor.IsStale(Alpha).ShouldBeFalse();

        clock.Advance(Hundredth);
        monitor.IsStale(Alpha).ShouldBeTrue();
    }

    [Fact]
    public void TheStaleWindowIsThreeMissedBeatsOfFifteenSeconds()
    {
        HerdrHeartbeatMonitor.HeartbeatInterval.ShouldBe(TimeSpan.FromSeconds(15));
        HerdrHeartbeatMonitor.MissedBeatsBeforeStale.ShouldBe(3);
        HerdrHeartbeatMonitor.StaleAfter.ShouldBe(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void ABeatRestartsTheSilenceCount()
    {
        var clock = new ManualTimeProvider(Start);
        var monitor = new HerdrHeartbeatMonitor(clock);
        monitor.Track(Alpha);

        clock.Advance(TimeSpan.FromSeconds(30));
        monitor.Record(Alpha);
        clock.Advance(JustBeforeStale);
        monitor.IsStale(Alpha).ShouldBeFalse();

        clock.Advance(Hundredth);
        monitor.IsStale(Alpha).ShouldBeTrue();
    }

    [Fact]
    public void ASessionThatWasNeverTrackedOrBeatenIsNeverStale()
    {
        var clock = new ManualTimeProvider(Start);
        var monitor = new HerdrHeartbeatMonitor(clock);

        clock.Advance(TimeSpan.FromHours(1));

        monitor.IsStale(Alpha).ShouldBeFalse();
    }

    [Fact]
    public void ABeatStartsTrackingASessionNotSeenBefore()
    {
        var clock = new ManualTimeProvider(Start);
        var monitor = new HerdrHeartbeatMonitor(clock);

        monitor.Record(Alpha);
        clock.Advance(JustBeforeStale);
        monitor.IsStale(Alpha).ShouldBeFalse();

        clock.Advance(Hundredth);
        monitor.IsStale(Alpha).ShouldBeTrue();
    }
}

using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies the sliding rate window on the injected clock, including its exact boundary.
/// </summary>
public sealed class AutomationRateLimitTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RateLimitRefusesOverCapAndAdmitsAfterTheWindow()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = Kit.Define(
            "paced",
            new ManualTrigger(),
            Kit.Counting(tally),
            rateLimit: new RateLimit(2, TimeSpan.FromMinutes(10)));
        var engine = Kit.Engine(clock, Kit.ConsentFor("paced"), new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("paced", true);

        (await engine.RunManualAsync("paced", Token)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        clock.Advance(TimeSpan.FromMinutes(1));
        (await engine.RunManualAsync("paced", Token)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);

        // Both admitted runs are inside the window one second before it closes, so the third run is over the cap.
        clock.Advance(TimeSpan.FromMinutes(8) + TimeSpan.FromSeconds(59));
        var overCap = await engine.RunManualAsync("paced", Token);
        overCap.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        overCap.Guard.ShouldBe("rate-limit");

        // One second later the first run has left the window, which ends exactly ten minutes after it started.
        clock.Advance(TimeSpan.FromSeconds(1));
        (await engine.RunManualAsync("paced", Token)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);

        // The run admitted at ten minutes counts again, so the window is full once more.
        (await engine.RunManualAsync("paced", Token)).Guard.ShouldBe("rate-limit");
        tally.Count.ShouldBe(3);
    }
}

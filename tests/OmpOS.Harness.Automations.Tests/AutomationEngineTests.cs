using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies the trigger kinds, the enable toggle and argument errors through the engine's observable outcomes.
/// </summary>
public sealed class AutomationEngineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DisabledByDefaultAndNeverRunsTheBody()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = Kit.Define("report", new ManualTrigger(), Kit.Counting(tally));
        var engine = Kit.Engine(clock, Kit.ConsentFor("report"), new InMemoryAutomationGuardStore(), definition);

        engine.IsEnabled("report").ShouldBeFalse();
        var refused = await engine.RunManualAsync("report", Token);

        refused.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        refused.Guard.ShouldBe("enable-toggle");
        tally.Count.ShouldBe(0);
    }

    [Fact]
    public async Task EnablingAnAutomationAdmitsItsRuns()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = Kit.Define("report", new ManualTrigger(), Kit.Counting(tally));
        var engine = Kit.Engine(clock, Kit.ConsentFor("report"), new InMemoryAutomationGuardStore(), definition);

        engine.SetEnabled("report", true);
        var run = await engine.RunManualAsync("report", Token);

        run.Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        run.Depth.ShouldBe(1);
        run.Origin.ShouldBe("manual");
        tally.Count.ShouldBe(1);
        engine.History().ShouldBe([run]);
    }

    [Fact]
    public async Task ExternalEventRunsOnlyAutomationsSubscribedToItsName()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var build = new Tally();
        var deploy = new Tally();
        var buildDefinition = Kit.Define("build", new EventTrigger("build"), Kit.Counting(build));
        var deployDefinition = Kit.Define("deploy", new EventTrigger("deploy"), Kit.Counting(deploy));
        var engine = Kit.Engine(
            clock,
            Kit.ConsentFor("build", "deploy"),
            new InMemoryAutomationGuardStore(),
            buildDefinition,
            deployDefinition);
        engine.SetEnabled("build", true);
        engine.SetEnabled("deploy", true);

        var results = await engine.DispatchEventAsync(new AutomationEvent("build", "webhook"), Token);

        results.Count.ShouldBe(1);
        results[0].AutomationId.ShouldBe("build");
        results[0].Origin.ShouldBe("webhook");
        results[0].Depth.ShouldBe(1);
        results[0].Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        build.Count.ShouldBe(1);
        deploy.Count.ShouldBe(0);
        (await engine.DispatchEventAsync(new AutomationEvent("unsubscribed", "webhook"), Token)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ScheduledAutomationRunsWhenItsScheduleIsDueOnTheClock()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var schedule = new AutomationSchedule(Kit.Start.AddHours(1), TimeSpan.FromHours(1));
        var definition = Kit.Define("hourly", new ScheduledTrigger(schedule), Kit.Counting(tally));
        var engine = Kit.Engine(clock, Kit.ConsentFor("hourly"), new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("hourly", true);

        (await engine.RunDueScheduledAsync(Token)).ShouldBeEmpty();

        clock.Advance(TimeSpan.FromHours(1));
        var first = await engine.RunDueScheduledAsync(Token);
        first.Count.ShouldBe(1);
        first[0].Origin.ShouldBe("schedule");
        first[0].Outcome.ShouldBe(AutomationRunOutcome.Succeeded);

        // Three due instants were missed (+2h, +3h, +4h); they coalesce into one run, and the next due instant moves past now.
        clock.Advance(TimeSpan.FromHours(3));
        (await engine.RunDueScheduledAsync(Token)).Count.ShouldBe(1);
        (await engine.RunDueScheduledAsync(Token)).ShouldBeEmpty();
        tally.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RunningAnUnknownAutomationIsAnError()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var engine = Kit.Engine(clock, new FakeConsentGate(), new InMemoryAutomationGuardStore());

        await Should.ThrowAsync<ArgumentException>(() => engine.RunManualAsync("missing", Token));
    }

    [Fact]
    public void TwoAutomationsWithTheSameIdentifierAreRejected()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var first = Kit.Define("dup", new ManualTrigger(), Kit.Counting(new Tally()));
        var second = Kit.Define("dup", new ManualTrigger(), Kit.Counting(new Tally()));

        Should.Throw<ArgumentException>(() => _ = Kit.Engine(clock, new FakeConsentGate(), new InMemoryAutomationGuardStore(), first, second));
    }

    [Fact]
    public void ANonPositiveScheduleIntervalIsRejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => _ = new AutomationSchedule(Kit.Start, TimeSpan.Zero));
    }

    [Fact]
    public void ANonPositiveRateLimitIsRejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => _ = new RateLimit(0, TimeSpan.FromMinutes(1)));
    }
}

using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies the pause, consent and guard-store guards: each refuses the run it guards, and a failing store never admits it.
/// </summary>
public sealed class AutomationGuardTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PausedAutomationNamesItsGuardAndHowToResume()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = Kit.Define("paused", new ManualTrigger(), Kit.Counting(tally));
        var engine = Kit.Engine(clock, Kit.ConsentFor("paused"), new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("paused", true);
        await engine.PauseAsync(
            "paused",
            new AutomationPause("loop-review", "two self-triggers in one hour", "call ResumeAsync after the review"),
            Token);

        var refused = await engine.RunManualAsync("paused", Token);

        refused.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        refused.Guard.ShouldBe("loop-review");
        var pause = refused.Pause.ShouldNotBeNull();
        pause.Guard.ShouldBe("loop-review");
        pause.HowToResume.ShouldBe("call ResumeAsync after the review");
        tally.Count.ShouldBe(0);

        await engine.ResumeAsync("paused", Token);
        (await engine.RunManualAsync("paused", Token)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        tally.Count.ShouldBe(1);
    }

    [Fact]
    public async Task FirstRunIsRefusedUntilPluginConsentIsGranted()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var consent = new FakeConsentGate();
        var definition = Kit.Define("gated", new ManualTrigger(), Kit.Counting(tally));
        var engine = Kit.Engine(clock, consent, new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("gated", true);

        var refused = await engine.RunManualAsync("gated", Token);
        refused.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        refused.Guard.ShouldBe("consent");
        tally.Count.ShouldBe(0);

        consent.Grant(Kit.HookFor("gated"));
        (await engine.RunManualAsync("gated", Token)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        tally.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AConsentGateThatCannotAnswerRefusesTheRun()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var consent = Kit.ConsentFor("gated");
        consent.Throws = true;
        var definition = Kit.Define("gated", new ManualTrigger(), Kit.Counting(tally));
        var engine = Kit.Engine(clock, consent, new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("gated", true);

        var run = await engine.RunManualAsync("gated", Token);

        run.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        run.Guard.ShouldBe("consent");
        tally.Count.ShouldBe(0);
    }

    [Fact]
    public async Task AFailingPauseLookupRefusesTheRunAndNeverRunsTheBody()
    {
        await RefusesWhenStoreFaultsAsync(GuardOperation.FindPause);
    }

    [Fact]
    public async Task AFailingRunCountRefusesTheRunAndNeverRunsTheBody()
    {
        await RefusesWhenStoreFaultsAsync(GuardOperation.CountRuns);
    }

    [Fact]
    public async Task AFailingRunRecordRefusesTheRunAndNeverRunsTheBody()
    {
        await RefusesWhenStoreFaultsAsync(GuardOperation.RecordRun);
    }

    [Fact]
    public async Task AGuardStoreFaultDoesNotLatchOnceTheStoreRecovers()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var store = new FaultInjectingGuardStore(new InMemoryAutomationGuardStore());
        var definition = Kit.Define("recovering", new ManualTrigger(), Kit.Counting(tally));
        var engine = Kit.Engine(clock, Kit.ConsentFor("recovering"), store, definition);
        engine.SetEnabled("recovering", true);

        store.Faults.Add(GuardOperation.FindPause);
        (await engine.RunManualAsync("recovering", Token)).Guard.ShouldBe("guard-store");

        store.Faults.Clear();
        (await engine.RunManualAsync("recovering", Token)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        tally.Count.ShouldBe(1);
    }

    private static async Task RefusesWhenStoreFaultsAsync(GuardOperation fault)
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var store = new FaultInjectingGuardStore(new InMemoryAutomationGuardStore());
        store.Faults.Add(fault);
        var definition = Kit.Define(
            "guarded",
            new ManualTrigger(),
            Kit.Counting(tally),
            rateLimit: new RateLimit(5, TimeSpan.FromHours(1)));
        var engine = Kit.Engine(clock, Kit.ConsentFor("guarded"), store, definition);
        engine.SetEnabled("guarded", true);

        var run = await engine.RunManualAsync("guarded", Token).ConfigureAwait(false);

        run.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        run.Guard.ShouldBe("guard-store");
        tally.Count.ShouldBe(0);
    }
}

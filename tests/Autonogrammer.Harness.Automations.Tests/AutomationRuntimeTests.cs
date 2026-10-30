using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Automations.Tests;

/// <summary>
/// Verifies the runtime bound on the injected clock: a run is cancelled exactly when its maximum runtime elapses.
/// </summary>
public sealed class AutomationRuntimeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunExceedingItsRuntimeBoundIsCancelledAndRecorded()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = Kit.Define("slow", new ManualTrigger(), WaitForCancellation(started), TimeSpan.FromMinutes(5));
        var engine = Kit.Engine(clock, Kit.ConsentFor("slow"), new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("slow", true);

        var run = engine.RunManualAsync("slow", Token);
        await started.Task.ConfigureAwait(true);
        clock.Advance(TimeSpan.FromMinutes(5));
        var record = await run.ConfigureAwait(true);

        record.Outcome.ShouldBe(AutomationRunOutcome.TimedOut);
        record.Guard.ShouldBe("max-runtime");
        engine.History().ShouldBe([record]);
    }

    [Fact]
    public async Task RunOneTickInsideItsBoundIsNotCancelled()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var definition = Kit.Define("edge", new ManualTrigger(), WaitForCancellation(started), TimeSpan.FromMinutes(5));
        var engine = Kit.Engine(clock, Kit.ConsentFor("edge"), new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("edge", true);

        var run = engine.RunManualAsync("edge", Token);
        await started.Task.ConfigureAwait(true);
        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(1));
        run.IsCompleted.ShouldBeFalse();

        clock.Advance(TimeSpan.FromSeconds(1));
        var record = await run.ConfigureAwait(true);
        record.Outcome.ShouldBe(AutomationRunOutcome.TimedOut);
    }

    [Fact]
    public async Task RunThatFinishesWithinItsBoundSucceeds()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = Kit.Define("quick", new ManualTrigger(), Kit.Counting(tally), TimeSpan.FromMinutes(5));
        var engine = Kit.Engine(clock, Kit.ConsentFor("quick"), new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("quick", true);

        var run = await engine.RunManualAsync("quick", Token).ConfigureAwait(true);

        run.Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        tally.Count.ShouldBe(1);
    }

    private static AutomationBody WaitForCancellation(TaskCompletionSource started) =>
        async (_, cancellationToken) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        };
}

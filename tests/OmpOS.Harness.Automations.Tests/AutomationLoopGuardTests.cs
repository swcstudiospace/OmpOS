using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies the loop guards: an automation may not be triggered by its own run, and chains stop at the depth cap.
/// </summary>
public sealed class AutomationLoopGuardTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SelfTriggerFromFirstRunIsBlockedAndRecorded()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = Kit.Define("loop", new EventTrigger("tick"), Kit.EmitThen("tick", tally));
        var engine = Kit.Engine(clock, Kit.ConsentFor("loop"), new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("loop", true);

        var run = await engine.RunManualAsync("loop", Token);

        run.Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        tally.Count.ShouldBe(1);
        var blocked = engine.History().Where(record => record.Outcome == AutomationRunOutcome.Blocked).ToList();
        blocked.Count.ShouldBe(1);
        blocked[0].AutomationId.ShouldBe("loop");
        blocked[0].Guard.ShouldBe("loop-guard");
        blocked[0].Depth.ShouldBe(2);
        blocked[0].Origin.ShouldBe("loop");
    }

    [Fact]
    public async Task ChainDepthIsCappedAtThree()
    {
        var clock = new FakeTimeProvider(Kit.Start);
        var a = new Tally();
        var b = new Tally();
        var c = new Tally();
        var d = new Tally();
        var engine = Kit.Engine(
            clock,
            Kit.ConsentFor("a", "b", "c", "d"),
            new InMemoryAutomationGuardStore(),
            Kit.Define("a", new ManualTrigger(), Kit.EmitThen("to-b", a)),
            Kit.Define("b", new EventTrigger("to-b"), Kit.EmitThen("to-c", b)),
            Kit.Define("c", new EventTrigger("to-c"), Kit.EmitThen("to-d", c)),
            Kit.Define("d", new EventTrigger("to-d"), Kit.Counting(d)));
        foreach (var id in new[] { "a", "b", "c", "d" })
        {
            engine.SetEnabled(id, true);
        }

        var run = await engine.RunManualAsync("a", Token);

        run.Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        a.Count.ShouldBe(1);
        b.Count.ShouldBe(1);
        c.Count.ShouldBe(1);
        d.Count.ShouldBe(0);

        var atDepthThree = engine.History().Single(record => string.Equals(record.AutomationId, "c", StringComparison.Ordinal));
        atDepthThree.Depth.ShouldBe(3);
        atDepthThree.Outcome.ShouldBe(AutomationRunOutcome.Succeeded);

        var blocked = engine.History().Single(record => record.Outcome == AutomationRunOutcome.Blocked);
        blocked.AutomationId.ShouldBe("d");
        blocked.Depth.ShouldBe(4);
        blocked.Guard.ShouldBe("chain-depth");
    }
}

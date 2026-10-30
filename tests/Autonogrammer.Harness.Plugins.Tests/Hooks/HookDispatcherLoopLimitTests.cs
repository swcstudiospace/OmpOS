using System.Text.Json;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies <c>loop_limit</c> for <c>stop</c> and <c>subagentStop</c> follow-ups and the <c>loop_count</c> the hook is sent.
/// </summary>
public sealed class HookDispatcherLoopLimitTests
{
    private const string Followup = """{ "followup_message": "keep going" }""";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static int LoopCountSent(HookInvocation invocation)
    {
        using var document = JsonDocument.Parse(invocation.StandardInput.ToArray());
        return document.RootElement.GetProperty("loop_count").GetInt32();
    }

    [Fact]
    public async Task FollowupsStopAtTheLimitAndTheHookSeesTheCountRise()
    {
        var hook = HookFixtures.Hook(HookEvent.Stop, loopLimit: 2);
        var runner = ScriptedCommandRunner.Always(ScriptedCommandRunner.Exit(0, Followup));
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        var decisions = new List<HookDecision>();
        for (var turn = 0; turn < 4; turn++)
        {
            decisions.Add(await rig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token));
        }

        decisions.Select(decision => decision.FollowupMessage).ShouldBe(["keep going", "keep going", null, null], ignoreOrder: false);
        runner.Invocations.Select(LoopCountSent).ShouldBe([0, 1, 2, 2], ignoreOrder: false);
        decisions[2].Records.Single().Note.ShouldNotBeNull().ShouldContain("loop limit reached", Case.Sensitive);
    }

    [Fact]
    public async Task ANewUserMessageResetsTheBudget()
    {
        var hook = HookFixtures.Hook(HookEvent.Stop, loopLimit: 1);
        var runner = ScriptedCommandRunner.Always(ScriptedCommandRunner.Exit(0, Followup));
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        var first = await rig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token);
        var second = await rig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token);
        rig.Session.NewGeneration();
        var third = await rig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token);

        first.FollowupMessage.ShouldNotBeNull();
        second.FollowupMessage.ShouldBeNull();
        third.FollowupMessage.ShouldNotBeNull();
    }

    [Fact]
    public async Task ANullLimitMeansNoCap()
    {
        var hook = HookFixtures.Hook(HookEvent.Stop, loopLimit: null);
        var runner = ScriptedCommandRunner.Always(ScriptedCommandRunner.Exit(0, Followup));
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        for (var turn = 0; turn < 12; turn++)
        {
            var decision = await rig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token);
            decision.FollowupMessage.ShouldBe("keep going");
        }
    }

    [Fact]
    public async Task AZeroLimitNeverAllowsAFollowup()
    {
        var hook = HookFixtures.Hook(HookEvent.Stop, loopLimit: 0);
        using var rig = await DispatcherRig.CreateAsync([hook], Token, ScriptedCommandRunner.Always(ScriptedCommandRunner.Exit(0, Followup)));

        var decision = await rig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token);

        decision.FollowupMessage.ShouldBeNull();
    }

    [Fact]
    public async Task AHookThatStopsAskingResetsItsOwnCounter()
    {
        var hook = HookFixtures.Hook(HookEvent.Stop, loopLimit: 2);
        var answers = new Queue<string>(["""{ "followup_message": "again" }""", "{}", """{ "followup_message": "again" }""", """{ "followup_message": "again" }""", """{ "followup_message": "again" }"""]);
        var runner = new ScriptedCommandRunner((_, _) => Task.FromResult(ScriptedCommandRunner.Exit(0, answers.Dequeue())));
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        var results = new List<string?>();
        for (var turn = 0; turn < 5; turn++)
        {
            results.Add((await rig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token)).FollowupMessage);
        }

        results.ShouldBe(["again", null, "again", "again", null], ignoreOrder: false);
    }

    [Fact]
    public async Task EachHookHasItsOwnCounter()
    {
        var one = HookFixtures.Hook(HookEvent.Stop, id: "one", loopLimit: 1);
        var two = HookFixtures.Hook(HookEvent.Stop, id: "two", loopLimit: 3);
        var runner = ScriptedCommandRunner.Always(ScriptedCommandRunner.Exit(0, Followup));
        using var rig = await DispatcherRig.CreateAsync([one, two], Token, runner);

        await rig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token);
        await rig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token);
        var notes = rig.Log.Recent(10).Select(record => (record.HookId, record.Note)).ToList();

        notes.ShouldContain(("one", "loop limit reached (1 of 1); follow-up dropped"));
        notes.Count(note => string.Equals(note.HookId, "two", StringComparison.Ordinal) && note.Note is not null).ShouldBe(0);
    }

    [Theory]
    [InlineData("completed", true)]
    [InlineData("error", false)]
    [InlineData("aborted", false)]
    public async Task ASubagentFollowupIsOnlyUsedWhenTheSubagentCompleted(string status, bool used)
    {
        var hook = HookFixtures.Hook(HookEvent.SubagentStop);
        using var rig = await DispatcherRig.CreateAsync([hook], Token, ScriptedCommandRunner.Always(ScriptedCommandRunner.Exit(0, Followup)));

        var decision = await rig.DispatchAsync(HookEvent.SubagentStop, $$"""{ "subagent_type": "explore", "status": "{{status}}" }""", Token);

        (decision.FollowupMessage is not null).ShouldBe(used);
    }

    [Fact]
    public async Task OtherEventsAreNotGivenALoopCount()
    {
        var hook = HookFixtures.Hook(HookEvent.PostToolUse);
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        await rig.DispatchAsync(HookEvent.PostToolUse, """{ "tool_name": "Shell" }""", Token);

        using var document = JsonDocument.Parse(runner.Invocations.Single().StandardInput.ToArray());
        document.RootElement.TryGetProperty("loop_count", out _).ShouldBeFalse();
    }
}

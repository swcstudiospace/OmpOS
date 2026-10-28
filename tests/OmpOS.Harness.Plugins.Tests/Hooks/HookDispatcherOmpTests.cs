using System.Text.Json;
using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies dispatch of the <c>omp</c> section: omp extension events go to their own hooks, whose result objects are merged.
/// </summary>
public sealed class HookDispatcherOmpTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EffectiveHook OmpHook(string ompEvent, string id = "omp", string? matcher = null, bool blocking = false) =>
        HookFixtures.Hook(HookEvent.PreToolUse, id: id, matcher: matcher, blocking: blocking) with
        {
            EventName = ompEvent,
            Event = null,
            IsOmpEvent = true,
        };

    private static async Task<(HookDecision Decision, DispatcherRig Rig, ScriptedCommandRunner Runner)> DispatchAsync(
        EffectiveHook hook,
        string ompEvent,
        string payload,
        string stdout = "",
        bool consent = true)
    {
        var runner = ScriptedCommandRunner.Always(ScriptedCommandRunner.Exit(0, stdout));
        var rig = await DispatcherRig.CreateAsync([hook], Token, runner, consent).ConfigureAwait(true);
        var decision = await rig.Dispatcher.DispatchOmpAsync(ompEvent, rig.Session, HookFixtures.Json(payload), Token).ConfigureAwait(true);
        return (decision, rig, runner);
    }

    [Fact]
    public async Task TheHookReceivesTheOmpEventPlusTheCommonFieldsAndItsResultObjectIsReturned()
    {
        var (decision, rig, runner) = await DispatchAsync(
            OmpHook("tool_call"),
            "tool_call",
            """{ "toolName": "bash", "toolCallId": "c1", "input": { "command": "ls" } }""",
            """{ "block": true, "reason": "no bash today", "input": { "command": "true" } }""");
        using var _ = rig;

        using var sent = JsonDocument.Parse(runner.Invocations.Single().StandardInput.ToArray());
        sent.RootElement.GetProperty("hook_event_name").GetString().ShouldBe("tool_call");
        sent.RootElement.GetProperty("toolName").GetString().ShouldBe("bash");
        sent.RootElement.GetProperty("conversation_id").GetString().ShouldBe("conv-1");
        decision.OmpResult!.Value.GetProperty("block").GetBoolean().ShouldBeTrue();
        decision.OmpResult!.Value.GetProperty("reason").GetString().ShouldBe("no bash today");
        decision.Records.Single().MatchedValue.ShouldBe("bash");
    }

    [Fact]
    public async Task TheMatcherTestsTheToolNameOfToolEvents()
    {
        var hook = OmpHook("tool_call", matcher: "^(bash|write)$");
        var (matched, first, _) = await DispatchAsync(hook, "tool_call", """{ "toolName": "write" }""", "{}");
        using var _first = first;
        var (skipped, second, runner) = await DispatchAsync(hook, "tool_call", """{ "toolName": "read" }""", "{}");
        using var _second = second;

        matched.HooksRun.ShouldBe(1);
        skipped.HooksRun.ShouldBe(0);
        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task ResultsOfSeveralHooksMergeKeyByKeyAndABlockStaysBlocked()
    {
        var blocker = OmpHook("tool_call", "a");
        var overrider = OmpHook("tool_call", "b");
        var outputs = new Queue<string>(["""{ "block": true, "reason": "first", "note": "n" }""", """{ "block": false, "reason": "second" }"""]);
        var runner = new ScriptedCommandRunner((_, _) => Task.FromResult(ScriptedCommandRunner.Exit(0, outputs.Dequeue())));
        using var rig = await DispatcherRig.CreateAsync([blocker, overrider], Token, runner);

        var decision = await rig.Dispatcher.DispatchOmpAsync("tool_call", rig.Session, HookFixtures.Json("""{ "toolName": "bash" }"""), Token);

        var result = decision.OmpResult!.Value;
        result.GetProperty("block").GetBoolean().ShouldBeTrue();
        result.GetProperty("reason").GetString().ShouldBe("second");
        result.GetProperty("note").GetString().ShouldBe("n");
    }

    [Theory]
    [InlineData("tool_call", "block")]
    [InlineData("before_subagent_spawn", "block")]
    [InlineData("input", "handled")]
    [InlineData("session_before_compact", "cancel")]
    public async Task AGuardingOmpHookWithoutConsentProducesTheOmpDenyShape(string ompEvent, string flag)
    {
        var (decision, rig, runner) = await DispatchAsync(OmpHook(ompEvent, blocking: true), ompEvent, """{ "toolName": "bash", "agent": "task" }""", consent: false);
        using var _ = rig;

        runner.SpawnCount.ShouldBe(0);
        decision.Cause.ShouldBe(HookDecisionCause.ConsentMissing);
        decision.OmpResult!.Value.GetProperty(flag).GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task ABlockingToolCallReasonCarriesTheDenialMessage()
    {
        var (decision, rig, _) = await DispatchAsync(OmpHook("tool_call", blocking: true), "tool_call", """{ "toolName": "bash" }""", consent: false);
        using var _ = rig;

        decision.OmpResult!.Value.GetProperty("reason").GetString().ShouldNotBeNull().ShouldContain("no consent", Case.Sensitive);
    }

    [Fact]
    public async Task ANonGuardingOmpHookWithoutConsentIsSkipped()
    {
        var (decision, rig, runner) = await DispatchAsync(OmpHook("message_end"), "message_end", "{}", consent: false);
        using var _ = rig;

        runner.SpawnCount.ShouldBe(0);
        decision.OmpResult.ShouldBeNull();
        decision.IsDenied.ShouldBeFalse();
    }

    [Fact]
    public async Task CursorEventsDoNotReachOmpHooksAndOmpEventsDoNotReachCursorHooks()
    {
        var cursor = HookFixtures.Hook(HookEvent.PreToolUse, id: "cursor");
        var omp = OmpHook("tool_call", "omp");
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([cursor, omp], Token, runner);

        await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);
        await rig.Dispatcher.DispatchOmpAsync("tool_call", rig.Session, HookFixtures.Json("{}"), Token);

        rig.Log.Recent(10).Select(record => record.HookId).ShouldBe(["cursor", "omp"], ignoreOrder: false);
    }

    [Fact]
    public async Task AnOmpHookThatPrintsNonJsonIsAnInvalidResponseThatDoesNotBlock()
    {
        var (decision, rig, _) = await DispatchAsync(OmpHook("tool_call"), "tool_call", """{ "toolName": "bash" }""", "oops");
        using var _ = rig;

        decision.OmpResult.ShouldBeNull();
        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.InvalidResponse);
    }
}

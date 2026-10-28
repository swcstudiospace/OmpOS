using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies everything that happens before a process is spawned (enabled flag, locations, matcher, quarantine, consent) and
/// what the hook is sent.
/// </summary>
public sealed class HookDispatcherGatingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AGuardingHookWithoutConsentDeniesAndNothingIsSpawned()
    {
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([HookFixtures.Hook(HookEvent.PreToolUse)], Token, runner, consent: false);

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, """{ "tool_name": "Shell" }""", Token);

        runner.SpawnCount.ShouldBe(0);
        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.ConsentMissing);
        decision.AgentMessage.ShouldNotBeNull().ShouldContain("no consent", Case.Sensitive);
        decision.Records.Single().Status.ShouldBe(HookRunStatus.AwaitingConsent);
        decision.HooksRun.ShouldBe(0);
    }

    [Theory]
    [InlineData(HookEvent.AfterShellExecution)]
    [InlineData(HookEvent.Stop)]
    [InlineData(HookEvent.BeforeSubmitPrompt)]
    [InlineData(HookEvent.SessionStart)]
    public async Task AnObservingOrControlHookWithoutConsentIsSkippedWithoutDenying(HookEvent hookEvent)
    {
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([HookFixtures.Hook(hookEvent)], Token, runner, consent: false);

        var decision = await rig.DispatchAsync(hookEvent, "{}", Token);

        runner.SpawnCount.ShouldBe(0);
        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.AwaitingConsent);
    }

    [Fact]
    public async Task ConsentIsCheckedAgainstTheHookHashSoAnEditedHookIsNotConsented()
    {
        var original = HookFixtures.Hook(HookEvent.PreToolUse, hash: "hash-of-original");
        var edited = original with { CommandHash = "hash-of-edited-command" };
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([edited], Token, runner, consent: false);
        await rig.State.RecordConsentAsync(original.CommandHash, Token);

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        runner.SpawnCount.ShouldBe(0);
        decision.Cause.ShouldBe(HookDecisionCause.ConsentMissing);
    }

    [Fact]
    public async Task AQuarantinedSourceDeniesAGuardAndSkipsAnObserverWithoutSpawning()
    {
        var runner = new ScriptedCommandRunner();
        var guard = HookFixtures.Hook(HookEvent.PreToolUse, id: "guard");
        var observer = HookFixtures.Hook(HookEvent.PostToolUse, id: "observer");
        using var rig = await DispatcherRig.CreateAsync([guard, observer], Token, runner);
        await rig.State.QuarantineAsync("cursor-project:abc", "earlier fault", Token);

        var denied = await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);
        var skipped = await rig.DispatchAsync(HookEvent.PostToolUse, "{}", Token);

        runner.SpawnCount.ShouldBe(0);
        denied.Cause.ShouldBe(HookDecisionCause.PluginQuarantined);
        denied.AgentMessage.ShouldNotBeNull().ShouldContain("earlier fault", Case.Sensitive);
        skipped.IsDenied.ShouldBeFalse();
        skipped.Records.Single().Status.ShouldBe(HookRunStatus.Skipped);
    }

    [Fact]
    public async Task QuarantineIsCheckedBeforeConsent()
    {
        var hook = HookFixtures.Hook(HookEvent.PreToolUse);
        using var rig = await DispatcherRig.CreateAsync([hook], Token, consent: false);
        await rig.State.QuarantineAsync("cursor-project:abc", "bad", Token);

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        decision.Cause.ShouldBe(HookDecisionCause.PluginQuarantined);
    }

    [Fact]
    public async Task AManagedHookWithTamperResistantFileRunsWithoutUserConsent()
    {
        var hook = HookFixtures.Hook(HookEvent.BeforeShellExecution, scope: HookScope.Managed, preConsented: true, pluginId: "managed");
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner, consent: false);

        await rig.DispatchAsync(HookEvent.BeforeShellExecution, """{ "command": "ls" }""", Token);

        runner.SpawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task APreConsentedHookOfAQuarantinedSourceStillDoesNotRun()
    {
        var hook = HookFixtures.Hook(HookEvent.BeforeShellExecution, scope: HookScope.Managed, preConsented: true, pluginId: "managed");
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner, consent: false);
        await rig.State.QuarantineAsync("managed", "fault", Token);

        var decision = await rig.DispatchAsync(HookEvent.BeforeShellExecution, """{ "command": "ls" }""", Token);

        runner.SpawnCount.ShouldBe(0);
        decision.IsDenied.ShouldBeTrue();
    }

    [Fact]
    public async Task DisabledHooksNeverRunAndAreNotReported()
    {
        var fileDisabled = HookFixtures.Hook(HookEvent.PreToolUse, id: "a", enabled: false);
        var stateDisabled = HookFixtures.Hook(HookEvent.PreToolUse, id: "b");
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([fileDisabled, stateDisabled], Token, runner);
        await rig.State.SetHookDisabledAsync("b", true, Token);

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        runner.SpawnCount.ShouldBe(0);
        decision.Records.ShouldBeEmpty();
        decision.Verdict.ShouldBeNull();
        decision.HooksRun.ShouldBe(0);
    }

    [Fact]
    public async Task OnlyHooksWhoseMatcherMatchesRunAndTheMatchedValueIsRecorded()
    {
        var shell = HookFixtures.Hook(HookEvent.PreToolUse, id: "shell", matcher: "Shell");
        var read = HookFixtures.Hook(HookEvent.PreToolUse, id: "read", matcher: "^Read$");
        var any = HookFixtures.Hook(HookEvent.PreToolUse, id: "any", matcher: "*");
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([shell, read, any], Token, runner);

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, """{ "tool_name": "Shell" }""", Token);

        decision.Records.Select(record => record.HookId).ShouldBe(["shell", "any"], ignoreOrder: false);
        decision.Records.ShouldAllBe(record => record.MatchedValue == "Shell");
        runner.SpawnCount.ShouldBe(2);
    }

    [Fact]
    public async Task ASpecificMatcherDoesNotMatchAnEventWithoutMatcherValue()
    {
        var hook = HookFixtures.Hook(HookEvent.SessionStart, matcher: "anything");
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        await rig.DispatchAsync(HookEvent.SessionStart, "{}", Token);

        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task AnInvalidMatcherSkipsTheHookAndDeniesOnlyWhenFailClosed()
    {
        var open = HookFixtures.Hook(HookEvent.PreToolUse, id: "open", matcher: "([");
        var closed = HookFixtures.Hook(HookEvent.PreToolUse, id: "closed", matcher: "([", failClosed: true);
        var runner = new ScriptedCommandRunner();
        using var first = await DispatcherRig.CreateAsync([open], Token, runner);
        using var second = await DispatcherRig.CreateAsync([closed], Token, runner);

        var openDecision = await first.DispatchAsync(HookEvent.PreToolUse, """{ "tool_name": "Shell" }""", Token);
        var closedDecision = await second.DispatchAsync(HookEvent.PreToolUse, """{ "tool_name": "Shell" }""", Token);

        runner.SpawnCount.ShouldBe(0);
        openDecision.IsDenied.ShouldBeFalse();
        openDecision.Records.Single().Status.ShouldBe(HookRunStatus.Skipped);
        closedDecision.Cause.ShouldBe(HookDecisionCause.InvalidMatcher);
    }

    [Fact]
    public async Task RunsInFiltersHooksByWhereTheSessionRuns()
    {
        var local = HookFixtures.Hook(HookEvent.Stop, id: "local", runsIn: HookLocations.Local);
        var remote = HookFixtures.Hook(HookEvent.Stop, id: "remote", runsIn: HookLocations.Remote);
        var both = HookFixtures.Hook(HookEvent.Stop, id: "both");
        using var localRig = await DispatcherRig.CreateAsync([local, remote, both], Token);
        using var remoteRig = await DispatcherRig.CreateAsync([local, remote, both], Token, session: HookFixtures.Session(remote: true));

        var onLaptop = await localRig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token);
        var inSandbox = await remoteRig.DispatchAsync(HookEvent.Stop, """{ "status": "completed" }""", Token);

        onLaptop.Records.Select(record => record.HookId).ShouldBe(["local", "both"], ignoreOrder: false);
        inSandbox.Records.Select(record => record.HookId).ShouldBe(["remote", "both"], ignoreOrder: false);
    }

    [Fact]
    public async Task AllMatchingHooksRunInMergeOrderEvenAfterADeny()
    {
        var first = HookFixtures.Hook(HookEvent.BeforeShellExecution, id: "1", command: "./one.sh");
        var second = HookFixtures.Hook(HookEvent.BeforeShellExecution, id: "2", command: "./two.sh");
        var third = HookFixtures.Hook(HookEvent.BeforeShellExecution, id: "3", command: "./three.sh");
        var runner = new ScriptedCommandRunner((invocation, _) => Task.FromResult(
            string.Equals(invocation.Command, "./one.sh", StringComparison.Ordinal) ? ScriptedCommandRunner.Exit(2, string.Empty, "denied first") : ScriptedCommandRunner.Exit(0, """{ "permission": "allow" }""")));
        using var rig = await DispatcherRig.CreateAsync([first, second, third], Token, runner);

        var decision = await rig.DispatchAsync(HookEvent.BeforeShellExecution, """{ "command": "ls" }""", Token);

        runner.Invocations.Select(invocation => invocation.Command).ShouldBe(["./one.sh", "./two.sh", "./three.sh"], ignoreOrder: false);
        decision.IsDenied.ShouldBeTrue();
        decision.HooksRun.ShouldBe(3);
    }

    [Fact]
    public async Task TheHookReceivesTheJsonPayloadOnStandardInputAndTheLogHoldsOnlyItsHash()
    {
        var hook = HookFixtures.Hook(HookEvent.BeforeShellExecution);
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        var decision = await rig.DispatchAsync(HookEvent.BeforeShellExecution, """{ "command": "echo TOP-SECRET-TOKEN-123", "cwd": "/work/project", "sandbox": false }""", Token);

        var invocation = runner.Invocations.Single();
        var sent = invocation.StandardInput.ToArray();
        using var document = JsonDocument.Parse(sent);
        document.RootElement.GetProperty("hook_event_name").GetString().ShouldBe("beforeShellExecution");
        document.RootElement.GetProperty("command").GetString().ShouldBe("echo TOP-SECRET-TOKEN-123");
        document.RootElement.GetProperty("conversation_id").GetString().ShouldBe("conv-1");
        var record = decision.Records.Single();
        record.PayloadSha256.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(sent)));
        var logged = string.Join('\n', rig.Log.Recent(10).Select(r => $"{r.HookId}|{r.Note}|{r.StandardOutputExcerpt}|{r.StandardErrorExcerpt}|{r.UserMessage}|{r.PayloadSha256}|{r.MatchedValue}"));
        logged.ShouldNotContain("TOP-SECRET-TOKEN-123", Case.Sensitive);
        Encoding.UTF8.GetString(sent).ShouldContain("TOP-SECRET-TOKEN-123", Case.Sensitive);
    }

    [Fact]
    public async Task EveryRecordIsAppendedToTheRunLog()
    {
        var hook = HookFixtures.Hook(HookEvent.PreToolUse);
        using var rig = await DispatcherRig.CreateAsync([hook], Token);

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, """{ "tool_name": "Shell" }""", Token);

        rig.Log.Recent(10).ShouldBe(decision.Records, ignoreOrder: false);
        decision.Records.Single().SessionId.ShouldBe("conv-1");
        decision.Records.Single().EventName.ShouldBe("preToolUse");
        decision.Records.Single().At.ShouldBe(rig.Clock.GetUtcNow());
    }

    [Fact]
    public async Task SwappingTheHookSetTakesEffectOnTheNextDispatch()
    {
        var hook = HookFixtures.Hook(HookEvent.PreToolUse, id: "old");
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);
        var replacement = HookFixtures.Hook(HookEvent.PreToolUse, id: "new");
        await rig.State.RecordConsentAsync(replacement.CommandHash, Token);

        rig.Dispatcher.Replace(HookFixtures.Set(replacement));
        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        decision.Records.Single().HookId.ShouldBe("new");
    }

    [Fact]
    public async Task DispatchingByNameIgnoresAnUnknownCursorEventAndRoutesKnownOnes()
    {
        var hook = HookFixtures.Hook(HookEvent.Stop);
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        var unknown = await rig.Dispatcher.DispatchNamedAsync("madeUp", false, rig.Session, HookFixtures.Json("{}"), Token);
        var known = await rig.Dispatcher.DispatchNamedAsync("stop", false, rig.Session, HookFixtures.Json("""{ "status": "completed" }"""), Token);

        unknown.ShouldBe(HookDecision.None);
        known.HooksRun.ShouldBe(1);
        runner.SpawnCount.ShouldBe(1);
    }
}

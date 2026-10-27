using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the failure policy table of design 3.5 case by case: what a hook's exit code, output, crash or timeout does to a
/// permission event, to an observing event, and with <c>failClosed</c>.
/// </summary>
public sealed class HookDispatcherFailurePolicyTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(HookDecision Decision, DispatcherRig Rig)> RunAsync(
        HookEvent hookEvent,
        HookCommandOutcome outcome,
        bool failClosed = false,
        string? command = "./check.sh",
        IReadOnlyList<string>? args = null)
    {
        var hook = HookFixtures.Hook(hookEvent, failClosed: failClosed, command: command, args: args);
        var rig = await DispatcherRig.CreateAsync([hook], Token, ScriptedCommandRunner.Always(outcome)).ConfigureAwait(true);
        var decision = await rig.DispatchAsync(hookEvent, """{ "command": "ls", "tool_name": "Shell" }""", Token).ConfigureAwait(true);
        return (decision, rig);
    }

    [Fact]
    public async Task ExitZeroWithAValidAllowUsesTheDecision()
    {
        var (decision, rig) = await RunAsync(HookEvent.PreToolUse, ScriptedCommandRunner.Exit(0, """{ "permission": "allow", "user_message": "ok" }"""));
        using var _ = rig;

        decision.Verdict.ShouldBe(HookVerdict.Allow);
        decision.UserMessage.ShouldBe("ok", Case.Sensitive);
        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Succeeded);
        decision.HooksRun.ShouldBe(1);
    }

    [Fact]
    public async Task ExitZeroWithADenyOnAPermissionEventDenies()
    {
        var (decision, rig) = await RunAsync(HookEvent.BeforeShellExecution, ScriptedCommandRunner.Exit(0, """{ "permission": "deny", "user_message": "no", "agent_message": "blocked" }"""));
        using var _ = rig;

        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.HookDenied);
        decision.AgentMessage.ShouldBe("blocked", Case.Sensitive);
        decision.UserMessage.ShouldBe("no", Case.Sensitive);
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Denied);
        decision.Records.Single().Decision.ShouldBe("deny", Case.Sensitive);
    }

    [Theory]
    [InlineData(HookEvent.PreToolUse)]
    [InlineData(HookEvent.SubagentStart)]
    [InlineData(HookEvent.BeforeShellExecution)]
    [InlineData(HookEvent.BeforeMcpExecution)]
    [InlineData(HookEvent.BeforeReadFile)]
    [InlineData(HookEvent.BeforeTabFileRead)]
    public async Task InvalidJsonOnEachPermissionEventDenies(HookEvent hookEvent)
    {
        var (decision, rig) = await RunAsync(hookEvent, ScriptedCommandRunner.Exit(0, "this is not json"));
        using var _ = rig;

        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.InvalidResponse);
        decision.Records.Single().Status.ShouldBe(HookRunStatus.InvalidResponse);
    }

    [Fact]
    public async Task ASchemaMismatchOnAPermissionEventDenies()
    {
        var (decision, rig) = await RunAsync(HookEvent.BeforeReadFile, ScriptedCommandRunner.Exit(0, """{ "permission": "ask" }"""));
        using var _ = rig;

        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.InvalidResponse);
    }

    [Theory]
    [InlineData(HookEvent.AfterShellExecution)]
    [InlineData(HookEvent.PostToolUse)]
    [InlineData(HookEvent.Stop)]
    [InlineData(HookEvent.BeforeSubmitPrompt)]
    [InlineData(HookEvent.SessionStart)]
    public async Task InvalidJsonOnANonPermissionEventIsLoggedAndIgnored(HookEvent hookEvent)
    {
        var (decision, rig) = await RunAsync(hookEvent, ScriptedCommandRunner.Exit(0, "<html>"), failClosed: true);
        using var _ = rig;

        decision.IsDenied.ShouldBeFalse();
        decision.Verdict.ShouldBeNull();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.InvalidResponse);
    }

    [Fact]
    public async Task ExitTwoDeniesAndTakesTheMessageFromStdoutJsonFirst()
    {
        var (decision, rig) = await RunAsync(HookEvent.PreToolUse, ScriptedCommandRunner.Exit(2, """{ "agent_message": "from json", "user_message": "u" }""", "from stderr"));
        using var _ = rig;

        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.ExitCodeTwo);
        decision.AgentMessage.ShouldBe("from json", Case.Sensitive);
        decision.UserMessage.ShouldBe("u", Case.Sensitive);
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Denied);
        decision.Records.Single().ExitCode.ShouldBe(2);
    }

    [Fact]
    public async Task ExitTwoFallsBackToStderrTextThenToAGenericMessage()
    {
        var (withStderr, first) = await RunAsync(HookEvent.BeforeShellExecution, ScriptedCommandRunner.Exit(2, string.Empty, "  git is blocked here\n"));
        using var _first = first;
        var (bare, second) = await RunAsync(HookEvent.BeforeShellExecution, ScriptedCommandRunner.Exit(2));
        using var _second = second;

        withStderr.AgentMessage.ShouldBe("git is blocked here", Case.Sensitive);
        bare.AgentMessage.ShouldContain("exit code 2", Case.Sensitive);
    }

    [Fact]
    public async Task ExitTwoOnAnObservingEventIsAFailureThatDoesNotBlock()
    {
        var (decision, rig) = await RunAsync(HookEvent.AfterFileEdit, ScriptedCommandRunner.Exit(2, string.Empty, "nope"));
        using var _ = rig;

        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Failed);
    }

    [Fact]
    public async Task ExitTwoOnBeforeSubmitPromptBlocksThePrompt()
    {
        var (decision, rig) = await RunAsync(HookEvent.BeforeSubmitPrompt, ScriptedCommandRunner.Exit(2, string.Empty, "secrets in prompt"));
        using var _ = rig;

        decision.IsDenied.ShouldBeTrue();
        decision.UserMessage.ShouldBe("secrets in prompt", Case.Sensitive);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(127)]
    [InlineData(139)]
    public async Task OtherExitCodesFailOpen(int exitCode)
    {
        var (decision, rig) = await RunAsync(HookEvent.PreToolUse, ScriptedCommandRunner.Exit(exitCode, string.Empty, "boom"));
        using var _ = rig;

        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Failed);
        decision.Records.Single().ExitCode.ShouldBe(exitCode);
    }

    [Fact]
    public async Task OtherExitCodesDenyWhenTheHookIsFailClosed()
    {
        var (decision, rig) = await RunAsync(HookEvent.PreToolUse, ScriptedCommandRunner.Exit(1, string.Empty, "boom"), failClosed: true);
        using var _ = rig;

        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.FailClosedFailure);
        decision.AgentMessage.ShouldContain("failClosed", Case.Sensitive);
    }

    [Fact]
    public async Task FailClosedHasNoEffectOnAnObservingEvent()
    {
        var (decision, rig) = await RunAsync(HookEvent.AfterShellExecution, ScriptedCommandRunner.Exit(1), failClosed: true);
        using var _ = rig;

        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Failed);
    }

    [Fact]
    public async Task NoOutputOnAPermissionEventAllowsUnlessFailClosed()
    {
        var (open, first) = await RunAsync(HookEvent.PreToolUse, ScriptedCommandRunner.Exit(0));
        using var _first = first;
        var (closed, second) = await RunAsync(HookEvent.PreToolUse, ScriptedCommandRunner.Exit(0), failClosed: true);
        using var _second = second;

        open.IsDenied.ShouldBeFalse();
        open.Records.Single().Status.ShouldBe(HookRunStatus.Succeeded);
        closed.IsDenied.ShouldBeTrue();
        closed.Cause.ShouldBe(HookDecisionCause.FailClosedFailure);
    }

    [Fact]
    public async Task ATruncatedResponseThatNoLongerParsesIsAFailureNotAnInvalidResponseDeny()
    {
        var truncated = new HookCommandOutcome(0, """{ "permission": "allow", "agent_message": "aaaa""", string.Empty, true, false);
        var (open, first) = await RunAsync(HookEvent.PreToolUse, truncated);
        using var _first = first;
        var (closed, second) = await RunAsync(HookEvent.PreToolUse, truncated, failClosed: true);
        using var _second = second;

        open.IsDenied.ShouldBeFalse();
        open.Records.Single().Status.ShouldBe(HookRunStatus.Failed);
        open.Records.Single().OutputTruncated.ShouldBeTrue();
        closed.Cause.ShouldBe(HookDecisionCause.FailClosedFailure);
    }

    [Fact]
    public async Task ATruncatedStreamWithAValidResponseIsUsedAsIs()
    {
        var chatty = new HookCommandOutcome(0, """{ "permission": "deny", "agent_message": "stop" }""", string.Empty, true, false);
        var (decision, rig) = await RunAsync(HookEvent.PreToolUse, chatty);
        using var _ = rig;

        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.HookDenied);
    }

    [Fact]
    public async Task AnArgsHookThatCannotStartFailsOpenOrClosedLikeACrash()
    {
        var missing = new HookCommandOutcome(-1, string.Empty, string.Empty, false, false, HookSpawnFailure.ExecutableNotFound, "no such file", false);
        var (open, first) = await RunAsync(HookEvent.PreToolUse, missing, command: null, args: ["./nope"]);
        using var _first = first;
        var (closed, second) = await RunAsync(HookEvent.PreToolUse, missing, failClosed: true, command: null, args: ["./nope"]);
        using var _second = second;

        open.IsDenied.ShouldBeFalse();
        open.Records.Single().Status.ShouldBe(HookRunStatus.Failed);
        closed.IsDenied.ShouldBeTrue();
        closed.Cause.ShouldBe(HookDecisionCause.FailClosedFailure);
        first.State.FindQuarantine("cursor-project:abc").ShouldBeNull();
    }

    [Fact]
    public async Task AMissingShellIsAHostFaultThatQuarantinesTheSourceAndDeniesAGuard()
    {
        var noShell = new HookCommandOutcome(-1, string.Empty, string.Empty, false, false, HookSpawnFailure.ExecutableNotFound, "/bin/sh missing", false);
        var (decision, rig) = await RunAsync(HookEvent.PreToolUse, noShell);
        using var _ = rig;

        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.RunnerFault);
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Faulted);
        rig.State.FindQuarantine("cursor-project:abc").ShouldNotBeNull();
    }

    [Fact]
    public async Task ARunnerThatThrowsQuarantinesTheSourceAndNextTimeTheGuardDeniesWithoutSpawning()
    {
        var runner = new ScriptedCommandRunner((_, _) => throw new InvalidOperationException("runner exploded"));
        var hook = HookFixtures.Hook(HookEvent.BeforeShellExecution);
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        var first = await rig.DispatchAsync(HookEvent.BeforeShellExecution, """{ "command": "ls" }""", Token);
        var second = await rig.DispatchAsync(HookEvent.BeforeShellExecution, """{ "command": "ls" }""", Token);

        first.IsDenied.ShouldBeTrue();
        first.Records.Single().Status.ShouldBe(HookRunStatus.Faulted);
        rig.State.FindQuarantine("cursor-project:abc")!.Reason.ShouldContain("runner exploded", Case.Sensitive);
        second.Cause.ShouldBe(HookDecisionCause.PluginQuarantined);
        second.Records.Single().Status.ShouldBe(HookRunStatus.PluginQuarantined);
        runner.SpawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task ARunnerFaultOnAnObservingEventQuarantinesButDoesNotDeny()
    {
        var runner = new ScriptedCommandRunner((_, _) => throw new InvalidOperationException("boom"));
        var hook = HookFixtures.Hook(HookEvent.AfterShellExecution);
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        var decision = await rig.DispatchAsync(HookEvent.AfterShellExecution, """{ "command": "ls" }""", Token);

        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Faulted);
        rig.State.FindQuarantine("cursor-project:abc").ShouldNotBeNull();
    }

    [Fact]
    public async Task ATimeoutKillsTheHookAndFailsOpen()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ScriptedCommandRunner(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            return ScriptedCommandRunner.Exit(0);
        });
        var hook = HookFixtures.Hook(HookEvent.PreToolUse, timeoutSeconds: 10);
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        var pending = rig.DispatchAsync(HookEvent.PreToolUse, """{ "tool_name": "Shell" }""", Token);
        await started.Task;
        rig.Clock.Advance(TimeSpan.FromSeconds(11));
        var decision = await pending;

        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.TimedOut);
        rig.State.FindQuarantine("cursor-project:abc").ShouldBeNull();
    }

    [Fact]
    public async Task ATimeoutOfAFailClosedHookDenies()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ScriptedCommandRunner(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            return ScriptedCommandRunner.Exit(0);
        });
        var hook = HookFixtures.Hook(HookEvent.BeforeMcpExecution, failClosed: true, timeoutSeconds: 5);
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner);

        var pending = rig.DispatchAsync(HookEvent.BeforeMcpExecution, """{ "tool_name": "x" }""", Token);
        await started.Task;
        rig.Clock.Advance(TimeSpan.FromSeconds(6));
        var decision = await pending;

        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.FailClosedTimeout);
        decision.Records.Single().Status.ShouldBe(HookRunStatus.TimedOut);
    }

    [Fact]
    public async Task CancellationPropagatesAndIsNotAFault()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ScriptedCommandRunner(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            return ScriptedCommandRunner.Exit(0);
        });
        using var rig = await DispatcherRig.CreateAsync([HookFixtures.Hook(HookEvent.PreToolUse)], Token, runner);

        var pending = rig.DispatchAsync(HookEvent.PreToolUse, "{}", cancellation.Token);
        await started.Task;
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () => await pending);
        rig.State.FindQuarantine("cursor-project:abc").ShouldBeNull();
    }

    [Fact]
    public async Task APluginManifestHookThatPrintsPlainTextOnExitZeroIsAllowed()
    {
        var hook = HookFixtures.Hook(HookEvent.PreToolUse, scope: HookScope.Plugin, pluginId: "acme", id: "acme#0");
        using var rig = await DispatcherRig.CreateAsync([hook], Token, ScriptedCommandRunner.Always(ScriptedCommandRunner.Exit(0, "checked 3 files\n")));

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        decision.IsDenied.ShouldBeFalse();
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Succeeded);
    }

    [Fact]
    public async Task AnInputOverTheLimitIsNotDeliveredAndCountsAsAFailure()
    {
        var hook = HookFixtures.Hook(HookEvent.PreToolUse, failClosed: true);
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([hook], Token, runner, options: new HookDispatcherOptions { MaxInputBytes = 64 });

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, """{ "tool_name": "Shell", "tool_input": { "command": "echo a very long command line that exceeds the cap" } }""", Token);

        runner.SpawnCount.ShouldBe(0);
        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.FailClosedFailure);
    }
}

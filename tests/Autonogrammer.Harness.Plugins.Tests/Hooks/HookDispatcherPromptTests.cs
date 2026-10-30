using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies prompt hooks: <c>$ARGUMENTS</c> substitution, the <c>{ ok, reason }</c> answer, failure and timeout handling and the
/// recursion guard.
/// </summary>
public sealed class HookDispatcherPromptTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class FakeEvaluator(Func<string, string?, CancellationToken, Task<PromptHookAnswer>> behavior) : IPromptHookEvaluator
    {
        public List<(string Prompt, string? Model)> Calls { get; } = [];

        public async ValueTask<PromptHookAnswer> EvaluateAsync(string prompt, string? model, CancellationToken cancellationToken)
        {
            Calls.Add((prompt, model));
            return await behavior(prompt, model, cancellationToken).ConfigureAwait(false);
        }
    }

    private static EffectiveHook PromptHook(string prompt, HookEvent hookEvent = HookEvent.PreToolUse, bool failClosed = false, int timeoutSeconds = 30) =>
        HookFixtures.Hook(hookEvent, type: HookType.Prompt, prompt: prompt, command: null, failClosed: failClosed, timeoutSeconds: timeoutSeconds);

    [Fact]
    public async Task ArgumentsAreReplacedByTheInputJsonAndTheAnswerIsUsed()
    {
        var evaluator = new FakeEvaluator((_, _, _) => Task.FromResult(new PromptHookAnswer(true, null)));
        var runner = new ScriptedCommandRunner();
        using var rig = await DispatcherRig.CreateAsync([PromptHook("Is this safe? $ARGUMENTS")], Token, runner, prompts: evaluator);

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, """{ "tool_name": "Shell" }""", Token);

        runner.SpawnCount.ShouldBe(0);
        evaluator.Calls.Single().Prompt.ShouldStartWith("Is this safe? {", Case.Sensitive);
        evaluator.Calls.Single().Prompt.ShouldContain("\"tool_name\":\"Shell\"", Case.Sensitive);
        evaluator.Calls.Single().Prompt.ShouldNotContain("$ARGUMENTS", Case.Sensitive);
        decision.Verdict.ShouldBe(HookVerdict.Allow);
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Succeeded);
    }

    [Fact]
    public void TheInputIsAppendedWhenThePromptHasNoPlaceholder()
    {
        HookPromptText.Build("Only allow reads.", "{\"a\":1}").ShouldBe("Only allow reads.\n\n{\"a\":1}");
        HookPromptText.Build("A $ARGUMENTS B $ARGUMENTS", "X").ShouldBe("A X B X");
    }

    [Fact]
    public async Task ANotOkAnswerDeniesWithTheReasonForTheAgent()
    {
        var evaluator = new FakeEvaluator((_, _, _) => Task.FromResult(new PromptHookAnswer(false, "writes outside the repo")));
        using var rig = await DispatcherRig.CreateAsync([PromptHook("p")], Token, prompts: evaluator);

        var decision = await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        decision.IsDenied.ShouldBeTrue();
        decision.Cause.ShouldBe(HookDecisionCause.HookDenied);
        decision.AgentMessage.ShouldBe("writes outside the repo");
        decision.Records.Single().Status.ShouldBe(HookRunStatus.Denied);
    }

    [Fact]
    public async Task ANotOkAnswerOnAnObservingEventDoesNotBlock()
    {
        var evaluator = new FakeEvaluator((_, _, _) => Task.FromResult(new PromptHookAnswer(false, "no")));
        using var rig = await DispatcherRig.CreateAsync([PromptHook("p", HookEvent.AfterFileEdit)], Token, prompts: evaluator);

        var decision = await rig.DispatchAsync(HookEvent.AfterFileEdit, "{}", Token);

        decision.IsDenied.ShouldBeFalse();
    }

    [Fact]
    public async Task WithoutAnEvaluatorAPromptHookFailsOpenOrClosed()
    {
        using var open = await DispatcherRig.CreateAsync([PromptHook("p")], Token);
        using var closed = await DispatcherRig.CreateAsync([PromptHook("p", failClosed: true)], Token);

        var openDecision = await open.DispatchAsync(HookEvent.PreToolUse, "{}", Token);
        var closedDecision = await closed.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        openDecision.IsDenied.ShouldBeFalse();
        openDecision.Records.Single().Status.ShouldBe(HookRunStatus.Failed);
        closedDecision.Cause.ShouldBe(HookDecisionCause.FailClosedFailure);
    }

    [Fact]
    public async Task AModelErrorCountsAsACrash()
    {
        var evaluator = new FakeEvaluator((_, _, _) => throw new InvalidOperationException("provider down"));
        using var open = await DispatcherRig.CreateAsync([PromptHook("p")], Token, prompts: evaluator);
        using var closed = await DispatcherRig.CreateAsync([PromptHook("p", failClosed: true)], Token, prompts: evaluator);

        var openDecision = await open.DispatchAsync(HookEvent.PreToolUse, "{}", Token);
        var closedDecision = await closed.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        openDecision.IsDenied.ShouldBeFalse();
        openDecision.Records.Single().Status.ShouldBe(HookRunStatus.Failed);
        closedDecision.IsDenied.ShouldBeTrue();
        open.State.FindQuarantine("cursor-project:abc").ShouldBeNull();
    }

    [Fact]
    public async Task APromptThatTakesTooLongTimesOut()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var evaluator = new FakeEvaluator(async (_, _, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token).ConfigureAwait(false);
            return new PromptHookAnswer(true, null);
        });
        using var rig = await DispatcherRig.CreateAsync([PromptHook("p", failClosed: true, timeoutSeconds: 4)], Token, prompts: evaluator);

        var pending = rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);
        await started.Task.ConfigureAwait(true);
        rig.Clock.Advance(TimeSpan.FromSeconds(5));
        var decision = await pending.ConfigureAwait(true);

        decision.Records.Single().Status.ShouldBe(HookRunStatus.TimedOut);
        decision.Cause.ShouldBe(HookDecisionCause.FailClosedTimeout);
    }

    [Fact]
    public async Task HooksAreDisabledWhileAPromptHooksModelCallRuns()
    {
        var innerRunner = new ScriptedCommandRunner();
        HookDecision? nested = null;
        HookDispatcher? dispatcher = null;
        HookSession? session = null;
        var evaluator = new FakeEvaluator(async (_, _, token) =>
        {
            nested = await dispatcher!.DispatchAsync(HookEvent.BeforeShellExecution, session!, HookFixtures.Json("""{ "command": "ls" }"""), token).ConfigureAwait(true);
            return new PromptHookAnswer(true, null);
        });
        var prompt = PromptHook("p");
        var command = HookFixtures.Hook(HookEvent.BeforeShellExecution, id: "guard");
        using var rig = await DispatcherRig.CreateAsync([prompt, command], Token, innerRunner, prompts: evaluator);
        dispatcher = rig.Dispatcher;
        session = rig.Session;

        await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);
        var after = await rig.DispatchAsync(HookEvent.BeforeShellExecution, """{ "command": "ls" }""", Token);

        nested.ShouldBe(HookDecision.None);
        innerRunner.SpawnCount.ShouldBe(1);
        after.HooksRun.ShouldBe(1);
    }

    [Fact]
    public async Task TheModelOverrideReachesTheEvaluator()
    {
        var evaluator = new FakeEvaluator((_, _, _) => Task.FromResult(new PromptHookAnswer(true, null)));
        var hook = PromptHook("p") with { Model = "small-model" };
        using var rig = await DispatcherRig.CreateAsync([hook], Token, prompts: evaluator);

        await rig.DispatchAsync(HookEvent.PreToolUse, "{}", Token);

        evaluator.Calls.Single().Model.ShouldBe("small-model");
    }
}

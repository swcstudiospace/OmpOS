using System.Text.Json;
using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the merge of several hook answers: deny beats ask beats allow, messages concatenate in run order, scalar fields
/// take the last responding hook, and each event honours only its own fields.
/// </summary>
public sealed class HookDispatcherMergeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<HookDecision> MergeAsync(HookEvent hookEvent, params string[] outputs)
    {
        var hooks = outputs.Select((_, index) => HookFixtures.Hook(hookEvent, id: $"h{index}", command: $"./h{index}.sh")).ToList();
        var runner = new ScriptedCommandRunner((invocation, _) =>
        {
            var index = int.Parse(invocation.Command!.AsSpan(3, 1), System.Globalization.CultureInfo.InvariantCulture);
            return Task.FromResult(ScriptedCommandRunner.Exit(0, outputs[index]));
        });
        using var rig = await DispatcherRig.CreateAsync(hooks, Token, runner).ConfigureAwait(true);
        return await rig.DispatchAsync(hookEvent, """{ "command": "ls", "tool_name": "Shell" }""", Token).ConfigureAwait(true);
    }

    [Theory]
    [InlineData("allow", "ask", "deny", HookVerdict.Deny)]
    [InlineData("deny", "allow", "allow", HookVerdict.Deny)]
    [InlineData("allow", "deny", "ask", HookVerdict.Deny)]
    [InlineData("allow", "ask", "allow", HookVerdict.Ask)]
    [InlineData("ask", "allow", "allow", HookVerdict.Ask)]
    [InlineData("allow", "allow", "allow", HookVerdict.Allow)]
    public async Task DenyBeatsAskBeatsAllowWhateverTheOrder(string first, string second, string third, HookVerdict expected)
    {
        var decision = await MergeAsync(
            HookEvent.BeforeShellExecution,
            $$"""{ "permission": "{{first}}" }""",
            $$"""{ "permission": "{{second}}" }""",
            $$"""{ "permission": "{{third}}" }""");

        decision.Verdict.ShouldBe(expected);
        decision.IsDenied.ShouldBe(expected == HookVerdict.Deny);
    }

    [Fact]
    public async Task HooksThatSayNothingLeaveTheVerdictUndecided()
    {
        var decision = await MergeAsync(HookEvent.PreToolUse, string.Empty, "{}");

        decision.Verdict.ShouldBeNull();
        decision.IsDenied.ShouldBeFalse();
    }

    [Fact]
    public async Task UserAndAgentMessagesAreConcatenatedInRunOrderWithANewline()
    {
        var decision = await MergeAsync(
            HookEvent.PreToolUse,
            """{ "permission": "deny", "user_message": "u1", "agent_message": "a1" }""",
            """{ "permission": "allow", "user_message": "u2" }""",
            """{ "agent_message": "a3" }""");

        decision.UserMessage.ShouldBe("u1\nu2", Case.Sensitive);
        decision.AgentMessage.ShouldBe("a1\na3", Case.Sensitive);
    }

    [Fact]
    public async Task ScalarFieldsTakeTheLastRespondingHookWhichIsALowerPrioritySource()
    {
        var decision = await MergeAsync(
            HookEvent.PreToolUse,
            """{ "updated_input": { "command": "first" } }""",
            """{ "updated_input": { "command": "second" } }""",
            "{}");

        decision.UpdatedInput!.Value.GetProperty("command").GetString().ShouldBe("second", Case.Sensitive);
    }

    [Fact]
    public async Task FollowupLastNonEmptyWinsAndEmptyDoesNotEraseAnEarlierOne()
    {
        var decision = await MergeAsync(
            HookEvent.Stop,
            """{ "followup_message": "run the tests" }""",
            """{ "followup_message": "" }""");

        decision.FollowupMessage.ShouldBe("run the tests", Case.Sensitive);
    }

    [Fact]
    public async Task AdditionalContextConcatenatesAndEnvironmentLaterHooksOverride()
    {
        var decision = await MergeAsync(
            HookEvent.SessionStart,
            """{ "additional_context": "ctx1", "env": { "A": "1", "B": "1" } }""",
            """{ "additional_context": "ctx2", "env": { "B": "2", "C": "3" } }""");

        decision.AdditionalContext.ShouldBe("ctx1\nctx2", Case.Sensitive);
        decision.Environment["A"].ShouldBe("1", Case.Sensitive);
        decision.Environment["B"].ShouldBe("2", Case.Sensitive);
        decision.Environment["C"].ShouldBe("3", Case.Sensitive);
    }

    [Fact]
    public async Task ContinueFalseFromAnyHookBlocksThePrompt()
    {
        var decision = await MergeAsync(
            HookEvent.BeforeSubmitPrompt,
            """{ "continue": true }""",
            """{ "continue": false, "user_message": "blocked by policy" }""",
            """{ "continue": true }""");

        decision.ShouldContinue.ShouldBe(false);
        decision.UserMessage.ShouldBe("blocked by policy", Case.Sensitive);
    }

    [Fact]
    public async Task ContinueTrueAloneIsReportedAndAbsenceIsNull()
    {
        (await MergeAsync(HookEvent.BeforeSubmitPrompt, """{ "continue": true }""")).ShouldContinue.ShouldBe(true);
        (await MergeAsync(HookEvent.BeforeSubmitPrompt, "{}")).ShouldContinue.ShouldBeNull();
    }

    [Fact]
    public async Task PluginPathsOfAllHooksAreCollected()
    {
        var decision = await MergeAsync(HookEvent.WorkspaceOpen, """{ "pluginPaths": ["/p/a"] }""", """{ "pluginPaths": ["/p/b", "/p/c"] }""");

        decision.PluginPaths.ShouldBe(["/p/a", "/p/b", "/p/c"], ignoreOrder: false);
    }

    [Fact]
    public async Task AnObservingEventIgnoresEveryOutputField()
    {
        var decision = await MergeAsync(HookEvent.AfterShellExecution, """{ "permission": "deny", "user_message": "x", "followup_message": "y" }""");

        decision.Verdict.ShouldBeNull();
        decision.UserMessage.ShouldBeNull();
        decision.FollowupMessage.ShouldBeNull();
        decision.IsDenied.ShouldBeFalse();
    }

    [Fact]
    public async Task ADecisionSerializesToTheCursorShapedSnakeCaseWireForm()
    {
        var decision = await MergeAsync(
            HookEvent.PreToolUse,
            """{ "permission": "deny", "user_message": "no", "agent_message": "blocked", "updated_input": { "command": "ls" } }""");

        using var wire = JsonDocument.Parse(decision.ToWireJson());
        var root = wire.RootElement;
        root.GetProperty("permission").GetString().ShouldBe("deny", Case.Sensitive);
        root.GetProperty("user_message").GetString().ShouldBe("no", Case.Sensitive);
        root.GetProperty("agent_message").GetString().ShouldBe("blocked", Case.Sensitive);
        root.GetProperty("updated_input").GetProperty("command").GetString().ShouldBe("ls", Case.Sensitive);
        root.GetProperty("hooks_run").GetInt32().ShouldBe(1);
        root.GetProperty("denial_cause").GetString().ShouldBe("HookDenied", Case.Sensitive);
        root.TryGetProperty("followup_message", out _).ShouldBeFalse();
        root.TryGetProperty("env", out _).ShouldBeFalse();
    }

    [Fact]
    public void TheEmptyDecisionSerializesToAnEmptyishObjectThatMeansAllow()
    {
        using var wire = JsonDocument.Parse(HookDecision.None.ToWireJson());

        wire.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(["hooks_run"], ignoreOrder: false);
    }
}

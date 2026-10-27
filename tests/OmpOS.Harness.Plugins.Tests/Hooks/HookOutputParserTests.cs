using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies parsing of hook standard output: Cursor's flat form, the Claude forms, per-event schema checks and the dropping of
/// fields an event does not honour.
/// </summary>
public sealed class HookOutputParserTests
{
    [Theory]
    [InlineData("")]
    [InlineData("  \n\t ")]
    public void BlankOutputIsEmptyNotInvalid(string output)
    {
        HookOutputParser.Parse(output, HookEvent.PreToolUse).Kind.ShouldBe(HookOutputKind.Empty);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("\"allow\"")]
    [InlineData("42")]
    [InlineData("{ \"permission\": \"deny\"")]
    public void OutputThatIsNotAJsonObjectIsInvalid(string output)
    {
        var parsed = HookOutputParser.Parse(output, HookEvent.BeforeShellExecution);

        parsed.Kind.ShouldBe(HookOutputKind.Invalid);
        parsed.Problem.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TheFlatPermissionFormIsParsedForAPermissionEvent()
    {
        var parsed = HookOutputParser.Parse(
            """{ "permission": "deny", "user_message": "no network", "agent_message": "do not call out", "updated_input": { "command": "npm ci" } }""",
            HookEvent.PreToolUse);

        parsed.Kind.ShouldBe(HookOutputKind.Valid);
        parsed.Verdict.ShouldBe(HookVerdict.Deny);
        parsed.UserMessage.ShouldBe("no network", Case.Sensitive);
        parsed.AgentMessage.ShouldBe("do not call out", Case.Sensitive);
        parsed.UpdatedInput!.Value.GetProperty("command").GetString().ShouldBe("npm ci", Case.Sensitive);
    }

    [Theory]
    [InlineData("""{ "permission": "maybe" }""")]
    [InlineData("""{ "permission": 1 }""")]
    [InlineData("""{ "user_message": 3 }""")]
    [InlineData("""{ "agent_message": {} }""")]
    [InlineData("""{ "continue": "yes" }""")]
    [InlineData("""{ "updated_input": "x" }""")]
    [InlineData("""{ "env": { "A": 1 } }""")]
    [InlineData("""{ "env": [] }""")]
    [InlineData("""{ "pluginPaths": "x" }""")]
    [InlineData("""{ "pluginPaths": [1] }""")]
    public void ATypeMismatchMakesTheOutputInvalid(string output)
    {
        HookOutputParser.Parse(output, HookEvent.BeforeShellExecution).Kind.ShouldBe(HookOutputKind.Invalid);
    }

    [Theory]
    [InlineData(HookEvent.BeforeShellExecution, HookVerdict.Ask)]
    [InlineData(HookEvent.BeforeMcpExecution, HookVerdict.Ask)]
    [InlineData(HookEvent.PreToolUse, HookVerdict.Allow)]
    [InlineData(HookEvent.SubagentStart, HookVerdict.Deny)]
    public void AskIsResolvedPerEvent(HookEvent hookEvent, HookVerdict expected)
    {
        var parsed = HookOutputParser.Parse("""{ "permission": "ask" }""", hookEvent);

        parsed.Kind.ShouldBe(HookOutputKind.Valid);
        parsed.Verdict.ShouldBe(expected);
    }

    [Theory]
    [InlineData(HookEvent.BeforeReadFile)]
    [InlineData(HookEvent.BeforeTabFileRead)]
    public void AskIsInvalidWhereTheSchemaDoesNotAcceptIt(HookEvent hookEvent)
    {
        HookOutputParser.Parse("""{ "permission": "ask" }""", hookEvent).Kind.ShouldBe(HookOutputKind.Invalid);
    }

    [Fact]
    public void FieldsAnEventDoesNotHonourAreDropped()
    {
        var parsed = HookOutputParser.Parse(
            """{ "permission": "deny", "user_message": "u", "agent_message": "a", "updated_input": {}, "followup_message": "f", "additional_context": "c", "continue": false, "env": { "A": "b" } }""",
            HookEvent.AfterShellExecution);

        parsed.Kind.ShouldBe(HookOutputKind.Valid);
        parsed.Verdict.ShouldBeNull();
        parsed.UserMessage.ShouldBeNull();
        parsed.AgentMessage.ShouldBeNull();
        parsed.UpdatedInput.ShouldBeNull();
        parsed.FollowupMessage.ShouldBeNull();
        parsed.AdditionalContext.ShouldBeNull();
        parsed.ShouldContinue.ShouldBeNull();
        parsed.Environment.ShouldBeEmpty();
    }

    [Fact]
    public void EachEventKeepsExactlyItsDocumentedOutputFields()
    {
        var all = """{ "permission": "allow", "user_message": "u", "agent_message": "a", "updated_input": {}, "updated_mcp_tool_output": {}, "followup_message": "f", "additional_context": "c", "continue": false, "env": { "A": "b" }, "pluginPaths": ["/p"] }""";

        var sessionStart = HookOutputParser.Parse(all, HookEvent.SessionStart);
        sessionStart.Environment["A"].ShouldBe("b", Case.Sensitive);
        sessionStart.AdditionalContext.ShouldBe("c", Case.Sensitive);
        sessionStart.Verdict.ShouldBeNull();

        var postToolUse = HookOutputParser.Parse(all, HookEvent.PostToolUse);
        postToolUse.UpdatedMcpToolOutput.ShouldNotBeNull();
        postToolUse.AdditionalContext.ShouldBe("c", Case.Sensitive);
        postToolUse.UpdatedInput.ShouldBeNull();

        var submit = HookOutputParser.Parse(all, HookEvent.BeforeSubmitPrompt);
        submit.ShouldContinue.ShouldBe(false);
        submit.UserMessage.ShouldBe("u", Case.Sensitive);
        submit.AgentMessage.ShouldBeNull();

        HookOutputParser.Parse(all, HookEvent.Stop).FollowupMessage.ShouldBe("f", Case.Sensitive);
        HookOutputParser.Parse(all, HookEvent.SubagentStop).FollowupMessage.ShouldBe("f", Case.Sensitive);
        HookOutputParser.Parse(all, HookEvent.PreCompact).UserMessage.ShouldBe("u", Case.Sensitive);
        HookOutputParser.Parse(all, HookEvent.WorkspaceOpen).PluginPaths.ShouldBe(["/p"], ignoreOrder: false);
        HookOutputParser.Parse(all, HookEvent.BeforeReadFile).AgentMessage.ShouldBeNull();
    }

    [Fact]
    public void TheClaudeNestedHookSpecificOutputFormIsAccepted()
    {
        var parsed = HookOutputParser.Parse(
            """{ "hookSpecificOutput": { "hookEventName": "PreToolUse", "permissionDecision": "deny", "permissionDecisionReason": "rm is blocked", "updatedInput": { "command": "ls" } } }""",
            HookEvent.PreToolUse);

        parsed.Kind.ShouldBe(HookOutputKind.Valid);
        parsed.Verdict.ShouldBe(HookVerdict.Deny);
        parsed.AgentMessage.ShouldBe("rm is blocked", Case.Sensitive);
        parsed.UpdatedInput!.Value.GetProperty("command").GetString().ShouldBe("ls", Case.Sensitive);
    }

    [Fact]
    public void AFlatFieldWinsOverTheNestedForm()
    {
        var parsed = HookOutputParser.Parse(
            """{ "permission": "allow", "hookSpecificOutput": { "permissionDecision": "deny" } }""",
            HookEvent.PreToolUse);

        parsed.Verdict.ShouldBe(HookVerdict.Allow);
    }

    [Theory]
    [InlineData("""{ "decision": "block", "reason": "keep going" }""", HookEvent.Stop, "keep going")]
    [InlineData("""{ "decision": "block", "reason": "keep going" }""", HookEvent.SubagentStop, "keep going")]
    public void TheLegacyBlockDecisionOfAStopHookBecomesAFollowup(string output, HookEvent hookEvent, string followup)
    {
        HookOutputParser.Parse(output, hookEvent).FollowupMessage.ShouldBe(followup, Case.Sensitive);
    }

    [Fact]
    public void TheLegacyBlockDecisionOfAPromptHookStopsTheSubmission()
    {
        var parsed = HookOutputParser.Parse("""{ "decision": "block", "reason": "no secrets" }""", HookEvent.BeforeSubmitPrompt);

        parsed.ShouldContinue.ShouldBe(false);
        parsed.UserMessage.ShouldBe("no secrets", Case.Sensitive);
    }

    [Theory]
    [InlineData("""{ "decision": "block", "reason": "no" }""", HookVerdict.Deny)]
    [InlineData("""{ "decision": "deny", "reason": "no" }""", HookVerdict.Deny)]
    [InlineData("""{ "decision": "approve" }""", HookVerdict.Allow)]
    [InlineData("""{ "decision": "allow" }""", HookVerdict.Allow)]
    public void TheLegacyDecisionOfAPermissionEventMapsToAVerdict(string output, HookVerdict expected)
    {
        HookOutputParser.Parse(output, HookEvent.PreToolUse).Verdict.ShouldBe(expected);
    }

    [Fact]
    public void OmpSectionOutputIsPassedThroughWhole()
    {
        var parsed = HookOutputParser.Parse("""{ "block": true, "reason": "r", "custom": [1,2] }""", null);

        parsed.Kind.ShouldBe(HookOutputKind.Valid);
        parsed.OmpObject!.Value.GetProperty("block").GetBoolean().ShouldBeTrue();
        parsed.OmpObject!.Value.GetProperty("custom").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public void OmpSectionOutputThatIsNotAnObjectIsInvalid()
    {
        HookOutputParser.Parse("[1]", null).Kind.ShouldBe(HookOutputKind.Invalid);
    }

    [Fact]
    public void NullValuedFieldsAreTreatedAsAbsent()
    {
        var parsed = HookOutputParser.Parse("""{ "permission": null, "user_message": null, "env": null }""", HookEvent.PreToolUse);

        parsed.Kind.ShouldBe(HookOutputKind.Valid);
        parsed.Verdict.ShouldBeNull();
    }
}

using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the catalogue of the 21 hook events against the facts in https://cursor.com/docs/hooks.
/// </summary>
public sealed class HookEventsTests
{
    public static TheoryData<HookEvent, string> WireNames => new()
    {
        { HookEvent.SessionStart, "sessionStart" },
        { HookEvent.SessionEnd, "sessionEnd" },
        { HookEvent.PreToolUse, "preToolUse" },
        { HookEvent.PostToolUse, "postToolUse" },
        { HookEvent.PostToolUseFailure, "postToolUseFailure" },
        { HookEvent.SubagentStart, "subagentStart" },
        { HookEvent.SubagentStop, "subagentStop" },
        { HookEvent.BeforeShellExecution, "beforeShellExecution" },
        { HookEvent.AfterShellExecution, "afterShellExecution" },
        { HookEvent.BeforeMcpExecution, "beforeMCPExecution" },
        { HookEvent.AfterMcpExecution, "afterMCPExecution" },
        { HookEvent.BeforeReadFile, "beforeReadFile" },
        { HookEvent.AfterFileEdit, "afterFileEdit" },
        { HookEvent.BeforeSubmitPrompt, "beforeSubmitPrompt" },
        { HookEvent.PreCompact, "preCompact" },
        { HookEvent.Stop, "stop" },
        { HookEvent.AfterAgentResponse, "afterAgentResponse" },
        { HookEvent.AfterAgentThought, "afterAgentThought" },
        { HookEvent.BeforeTabFileRead, "beforeTabFileRead" },
        { HookEvent.AfterTabFileEdit, "afterTabFileEdit" },
        { HookEvent.WorkspaceOpen, "workspaceOpen" },
    };

    [Theory]
    [MemberData(nameof(WireNames))]
    public void EveryEventRoundTripsThroughItsWireName(HookEvent hookEvent, string name)
    {
        HookEvents.Format(hookEvent).ShouldBe(name);
        HookEvents.TryParse(name, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(hookEvent);
    }

    [Fact]
    public void TheCatalogueHoldsExactlyTwentyOneEventsAndEveryEnumValueIsListed()
    {
        HookEvents.All.Count.ShouldBe(21);
        HookEvents.All.Select(info => info.Event).Distinct().Count().ShouldBe(Enum.GetValues<HookEvent>().Length);
        HookEvents.All.Select(info => info.Name).Distinct(StringComparer.Ordinal).Count().ShouldBe(21);
    }

    [Theory]
    [InlineData("PreToolUse")]
    [InlineData("beforemcpexecution")]
    [InlineData("")]
    [InlineData("sessionstart")]
    [InlineData("notAnEvent")]
    public void NamesAreMatchedExactlyAndCaseSensitively(string name)
    {
        HookEvents.TryParse(name, out _).ShouldBeFalse();
    }

    [Fact]
    public void NullNameIsNotAnEvent()
    {
        HookEvents.TryParse(null, out _).ShouldBeFalse();
    }

    [Fact]
    public void ExactlyTheSixPermissionEventsOfTheCursorDocsArePermissionEvents()
    {
        var permission = HookEvents.All.Where(info => info.Kind == HookEventKind.Permission).Select(info => info.Name).Order(StringComparer.Ordinal).ToList();

        permission.ShouldBe(
            ["beforeMCPExecution", "beforeReadFile", "beforeShellExecution", "beforeTabFileRead", "preToolUse", "subagentStart"],
            ignoreOrder: false);
    }

    [Fact]
    public void OnlyTheTabEventsCannotBeProducedByOmpOs()
    {
        HookEvents.All.Where(info => !info.CanProduceInOmpOs).Select(info => info.Event)
            .ShouldBe([HookEvent.BeforeTabFileRead, HookEvent.AfterTabFileEdit], ignoreOrder: true);
    }

    [Fact]
    public void BeforeSubmitPromptCanBlockEvenThoughItIsNotAPermissionEvent()
    {
        HookEvents.Info(HookEvent.BeforeSubmitPrompt).CanBlock.ShouldBeTrue();
        HookEvents.Info(HookEvent.BeforeSubmitPrompt).Kind.ShouldBe(HookEventKind.Control);
        HookEvents.Info(HookEvent.PostToolUse).CanBlock.ShouldBeFalse();
        HookEvents.Info(HookEvent.Stop).CanBlock.ShouldBeFalse();
    }

    [Theory]
    [InlineData(HookEvent.BeforeReadFile, "Read")]
    [InlineData(HookEvent.AfterFileEdit, "Write")]
    [InlineData(HookEvent.BeforeTabFileRead, "TabRead")]
    [InlineData(HookEvent.AfterTabFileEdit, "TabWrite")]
    [InlineData(HookEvent.BeforeSubmitPrompt, "UserPromptSubmit")]
    [InlineData(HookEvent.Stop, "Stop")]
    [InlineData(HookEvent.AfterAgentResponse, "AgentResponse")]
    [InlineData(HookEvent.AfterAgentThought, "AgentThought")]
    public void FixedMatcherValuesFollowTheCursorDocs(HookEvent hookEvent, string expected)
    {
        HookEvents.MatcherValue(hookEvent, HookFixtures.Json("{}")).ShouldBe(expected);
    }

    [Theory]
    [InlineData(HookEvent.PreToolUse, """{"tool_name":"Shell","tool_input":{}}""", "Shell")]
    [InlineData(HookEvent.PostToolUse, """{"tool_name":"MCP:linear_get"}""", "MCP:linear_get")]
    [InlineData(HookEvent.PostToolUseFailure, """{"tool_name":"Read"}""", "Read")]
    [InlineData(HookEvent.SubagentStart, """{"subagent_type":"explore"}""", "explore")]
    [InlineData(HookEvent.SubagentStop, """{"subagent_type":"shell"}""", "shell")]
    [InlineData(HookEvent.BeforeShellExecution, """{"command":"curl https://x"}""", "curl https://x")]
    [InlineData(HookEvent.AfterShellExecution, """{"command":"ls"}""", "ls")]
    [InlineData(HookEvent.BeforeMcpExecution, """{"tool_name":"create_issue"}""", "create_issue")]
    public void PayloadMatcherValuesComeFromTheDocumentedField(HookEvent hookEvent, string payload, string expected)
    {
        HookEvents.MatcherValue(hookEvent, HookFixtures.Json(payload)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(HookEvent.SessionStart)]
    [InlineData(HookEvent.SessionEnd)]
    [InlineData(HookEvent.PreCompact)]
    [InlineData(HookEvent.WorkspaceOpen)]
    public void EventsWithoutADocumentedMatcherHaveNoMatcherValue(HookEvent hookEvent)
    {
        HookEvents.MatcherValue(hookEvent, HookFixtures.Json("""{"command":"x","tool_name":"y"}""")).ShouldBeNull();
    }

    [Fact]
    public void MissingOrNonStringMatcherFieldGivesNoValue()
    {
        HookEvents.MatcherValue(HookEvent.PreToolUse, HookFixtures.Json("""{"tool_name":7}""")).ShouldBeNull();
        HookEvents.MatcherValue(HookEvent.PreToolUse, HookFixtures.Json("""{}""")).ShouldBeNull();
        HookEvents.MatcherValue(HookEvent.PreToolUse, HookFixtures.Json("[]")).ShouldBeNull();
    }

    [Fact]
    public void InfoOfAnUndefinedEventThrows()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => HookEvents.Info((HookEvent)999));
    }

    [Fact]
    public void AskIsHandledPerEventAsTheCursorDocsDescribe()
    {
        HookEvents.Info(HookEvent.PreToolUse).AskHandling.ShouldBe(HookAskHandling.AsAllow);
        HookEvents.Info(HookEvent.SubagentStart).AskHandling.ShouldBe(HookAskHandling.AsDeny);
        HookEvents.Info(HookEvent.BeforeShellExecution).AskHandling.ShouldBe(HookAskHandling.Enforced);
        HookEvents.Info(HookEvent.BeforeMcpExecution).AskHandling.ShouldBe(HookAskHandling.Enforced);
        HookEvents.Info(HookEvent.BeforeReadFile).AskHandling.ShouldBe(HookAskHandling.Invalid);
    }
}

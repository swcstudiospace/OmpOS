using OmpOS.Harness.Abstractions;
using OmpOS.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Safety;

/// <summary>Reading omp's permission requests: the ACP kind picks the call, several spellings of the fields are accepted, and anything unreadable asks.</summary>
public sealed class RunModeRequestParserTests
{
    private static RunModeRequest Parse(string toolCall, string name = "tool") =>
        RunModeRequestParser.Parse(new PermissionRequestEvent("c1", name, "{\"sessionId\":\"s\",\"toolCall\":" + toolCall + "}", "7"));

    [Fact]
    public void AnExecuteCallIsAShellRequestWithItsCommandAndWorkingDirectory()
    {
        var request = Parse("""{ "toolCallId": "c1", "title": "Run tests", "kind": "execute", "rawInput": { "command": "npm test", "cwd": "/w/pkg" } }""");

        request.Kind.ShouldBe(RunModeToolKind.Shell);
        request.Command.ShouldBe("npm test");
        request.WorkingDirectory.ShouldBe("/w/pkg");
        request.ToolName.ShouldBe("Run tests");
    }

    [Fact]
    public void ACommandGivenAsAnArrayIsJoined()
    {
        Parse("""{ "kind": "execute", "rawInput": { "cmd": ["git", "status", 3] } }""").Command.ShouldBe("git status");
    }

    [Fact]
    public void AnExecuteCallWithNoCommandKeepsAnUnknownCommandSoItAsks()
    {
        var request = Parse("""{ "kind": "execute", "rawInput": { "unexpected": 1 } }""");

        request.Kind.ShouldBe(RunModeToolKind.Shell);
        request.Command.ShouldBeNull();
        Parse("""{ "kind": "execute" }""").Command.ShouldBeNull();
    }

    [Theory]
    [InlineData("read", RunModeToolKind.Read)]
    [InlineData("search", RunModeToolKind.Read)]
    [InlineData("edit", RunModeToolKind.Write)]
    [InlineData("delete", RunModeToolKind.Write)]
    [InlineData("move", RunModeToolKind.Write)]
    [InlineData("fetch", RunModeToolKind.Fetch)]
    [InlineData("think", RunModeToolKind.Other)]
    [InlineData("switch_mode", RunModeToolKind.Other)]
    [InlineData("", RunModeToolKind.Other)]
    public void TheAcpKindPicksTheKindOfCall(string kind, RunModeToolKind expected)
    {
        Parse("{ \"kind\": \"" + kind + "\" }").Kind.ShouldBe(expected);
    }

    [Fact]
    public void FilesComeFromLocationsAndFromSeveralInputFieldsWithoutDuplicates()
    {
        var request = Parse("""
            { "kind": "move", "locations": [ { "path": "/w/a.txt" }, { "path": "/w/b.txt" } ],
              "rawInput": { "source": "/w/a.txt", "destination": "/w/c.txt", "paths": ["/w/d.txt", 4] } }
            """);

        request.Kind.ShouldBe(RunModeToolKind.Write);
        request.Paths.ShouldBe(["/w/a.txt", "/w/b.txt", "/w/d.txt", "/w/c.txt"]);  // locations first, then the input fields in the order the parser reads them
    }

    [Fact]
    public void AReadWithNoFileHasNoPaths()
    {
        Parse("""{ "kind": "read" }""").Paths.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public void AFetchCarriesItsAddress()
    {
        Parse("""{ "kind": "fetch", "rawInput": { "url": "https://example.com/a" } }""").FetchAddress.ShouldBe("https://example.com/a");
        Parse("""{ "kind": "fetch", "rawInput": { "uri": "https://example.com/b" } }""").FetchAddress.ShouldBe("https://example.com/b");
        Parse("""{ "kind": "fetch" }""").FetchAddress.ShouldBeNull();
    }

    [Theory]
    [InlineData("mcp__github_list_issues", "github", "list_issues")]
    [InlineData("mcp__linear__save_issue", "linear", "save_issue")]
    public void AnMcpToolNameMakesAnMcpRequestWhateverTheKind(string name, string server, string tool)
    {
        var request = Parse("{ \"kind\": \"other\", \"title\": \"" + name + "\" }");

        request.Kind.ShouldBe(RunModeToolKind.Mcp);
        request.McpServer.ShouldBe(server);
        request.McpTool.ShouldBe(tool);
        Parse("{ \"kind\": \"execute\", \"toolName\": \"" + name + "\", \"title\": \"Run\" }").Kind.ShouldBe(RunModeToolKind.Mcp);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{ "toolCall": 5 }""")]
    [InlineData("""{ "toolCall": null }""")]
    public void APayloadThatCannotBeReadBecomesACallOfUnknownKindAndNothingThrows(string payload)
    {
        var request = RunModeRequestParser.Parse(new PermissionRequestEvent("c9", "Mystery", payload));

        request.Kind.ShouldBe(RunModeToolKind.Other);
        request.ToolName.ShouldBe("Mystery");
        request.Command.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{ "toolCall": { "kind": "execute" } }""", "execute")]
    [InlineData("""{ "toolCall": { "kind": "read", "title": "x" } }""", "read")]
    [InlineData("""{ "toolCall": { "title": "no kind" } }""", "")]
    [InlineData("""{ "toolCall": 5 }""", "")]
    [InlineData("not json", "")]
    public void TheAcpKindIsReadForTheApprovalPolicy(string payload, string expected)
    {
        RunModeRequestParser.AcpKind(new PermissionRequestEvent("c", "t", payload)).ShouldBe(expected);
    }

    [Fact]
    public void RunEverythingNeedsAnApprovalPolicyThatAdmitsEveryAcpKind()
    {
        RunModeLaunchMapper.AcpToolKinds.ShouldBe(["read", "edit", "delete", "move", "search", "execute", "think", "fetch", "switch_mode", "other"]);
    }

    [Fact]
    public void ANullRequestIsRejected()
    {
        Should.Throw<ArgumentNullException>(() => RunModeRequestParser.Parse(null!));
        Should.Throw<ArgumentNullException>(() => RunModeRequestParser.AcpKind(null!));
    }
}

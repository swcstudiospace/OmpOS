using System.Text.Json;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>The mapping of ACP tool kinds to Cursor tool types and the description of a whole-file write as edits.</summary>
public sealed class AcpHookPayloadsTests
{
    /// <summary>Each ACP kind maps to the tool type a Cursor matcher names, an MCP title to <c>MCP:&lt;tool&gt;</c>, and an unknown kind to itself.</summary>
    [Theory]
    [InlineData("execute", "git status", "Shell")]
    [InlineData("read", "Read a.txt", "Read")]
    [InlineData("edit", "Edit a.txt", "Write")]
    [InlineData("move", "Move a.txt", "Write")]
    [InlineData("delete", "Delete a.txt", "Delete")]
    [InlineData("search", "grep x", "Grep")]
    [InlineData("fetch", "fetch page", "WebSearch")]
    [InlineData("think", "plan", "Task")]
    [InlineData("other", "something", "other")]
    [InlineData(null, "my tool", "my tool")]
    [InlineData(null, null, "Task")]
    [InlineData("other", "mcp__github_create_issue", "MCP:create_issue")]
    public void KindsMapToCursorToolTypes(string? kind, string? title, string expected) =>
        AcpHookPayloads.ToolName(kind, title).ShouldBe(expected);

    /// <summary>The command of an execute call is its raw input, else its title.</summary>
    [Fact]
    public void TheCommandComesFromTheRawInputElseTheTitle()
    {
        using var input = JsonDocument.Parse("{\"command\":\"ls -la\"}");
        using var other = JsonDocument.Parse("{\"path\":\"a\"}");

        AcpHookPayloads.Command(input.RootElement.Clone(), "title").ShouldBe("ls -la");
        AcpHookPayloads.Command(other.RootElement.Clone(), "the title").ShouldBe("the title");
        AcpHookPayloads.Command(null, null).ShouldBeEmpty();
    }

    /// <summary>A write is one edit holding what differs, with the shared head and tail removed; a new file is an edit from nothing; identical text is no edit.</summary>
    [Theory]
    [InlineData("hello world", "hello there", "world", "there")]
    [InlineData("a\nb\nc", "a\nX\nc", "b", "X")]
    [InlineData("aa", "aaa", "", "a")]
    [InlineData("abc", "", "abc", "")]
    public void AWriteIsDescribedByWhatDiffers(string previous, string content, string oldString, string newString)
    {
        var edit = AcpHookPayloads.Edits(previous, content).ShouldHaveSingleItem();

        edit.OldString.ShouldBe(oldString);
        edit.NewString.ShouldBe(newString);
    }

    /// <summary>A file that did not exist is reported as an edit from the empty string, and identical text produces no edit.</summary>
    [Fact]
    public void NewAndUnchangedFilesAreDescribedPlainly()
    {
        var created = AcpHookPayloads.Edits(null, "fresh").ShouldHaveSingleItem();
        created.ShouldBe(new HookFileEdit(string.Empty, "fresh"));
        AcpHookPayloads.Edits("same", "same").ShouldBeEmpty();
    }

    /// <summary>An edit larger than the cap is cut, so a huge rewrite never makes a payload the hook cannot take.</summary>
    [Fact]
    public void AHugeEditIsCut()
    {
        var edit = AcpHookPayloads.Edits(new string('a', 10), new string('b', AcpHookPayloads.MaxEditChars + 50)).ShouldHaveSingleItem();

        edit.NewString.Length.ShouldBe(AcpHookPayloads.MaxEditChars);
    }

    /// <summary>The tool payloads carry the tool type, the raw input as an object (an empty one when there is none), the tool use id and the working directory.</summary>
    [Fact]
    public void ToolPayloadsCarryTheirFields()
    {
        using var input = JsonDocument.Parse("{\"command\":\"ls\"}");

        var pre = AcpHookPayloads.PreToolUse("Shell", input.RootElement.Clone(), "c1", "/w");
        var post = AcpHookPayloads.PostToolUse("Shell", null, "out", "c1", "/w", TimeSpan.FromMilliseconds(250));
        var failure = AcpHookPayloads.PostToolUseFailure("Shell", null, "boom", "c1", "/w", TimeSpan.FromSeconds(1));

        pre.GetProperty("tool_input").GetProperty("command").GetString().ShouldBe("ls");
        pre.GetProperty("tool_use_id").GetString().ShouldBe("c1");
        post.GetProperty("tool_input").ValueKind.ShouldBe(JsonValueKind.Object);
        post.GetProperty("duration").GetInt64().ShouldBe(250);
        using var output = JsonDocument.Parse(post.GetProperty("tool_output").GetString()!);
        output.RootElement.GetProperty("content")[0].GetString().ShouldBe("out");
        output.RootElement.GetProperty("isError").GetBoolean().ShouldBeFalse();
        failure.GetProperty("error_message").GetString().ShouldBe("boom");
        failure.GetProperty("failure_type").GetString().ShouldBe("error");
        failure.GetProperty("is_interrupt").GetBoolean().ShouldBeFalse();
        failure.GetProperty("duration").GetInt64().ShouldBe(1000);
    }
}

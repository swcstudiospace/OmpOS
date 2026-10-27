using System.Text.Json;
using OmpOS.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies that the host-built payloads carry exactly the field names of https://cursor.com/docs/hooks.
/// </summary>
public sealed class HookPayloadsTests
{
    private static List<string> Names(JsonElement payload) => [.. payload.EnumerateObject().Select(property => property.Name)];

    [Fact]
    public void SessionStartAndEndCarryTheirDocumentedFields()
    {
        Names(HookPayloads.SessionStart("s1", true, "ask")).ShouldBe(["session_id", "is_background_agent", "composer_mode"], ignoreOrder: false);
        Names(HookPayloads.SessionStart("s1", composerMode: null)).ShouldBe(["session_id", "is_background_agent"], ignoreOrder: false);
        Names(HookPayloads.SessionEnd("s1", "error", 4500, false, "failed", "boom")).ShouldBe(
            ["session_id", "reason", "duration_ms", "is_background_agent", "final_status", "error_message"],
            ignoreOrder: false);
        Names(HookPayloads.SessionEnd("s1", "completed", 1, false, "ok")).ShouldNotContain("error_message");
    }

    [Fact]
    public void PromptStopAndCompactionPayloadsCarryTheirDocumentedFields()
    {
        var prompt = HookPayloads.BeforeSubmitPrompt("hello", [new HookAttachment("file", "/a.cs"), new HookAttachment("rule", "/r.md")]);
        prompt.GetProperty("prompt").GetString().ShouldBe("hello", Case.Sensitive);
        prompt.GetProperty("attachments")[1].GetProperty("type").GetString().ShouldBe("rule", Case.Sensitive);
        prompt.GetProperty("attachments")[0].GetProperty("file_path").GetString().ShouldBe("/a.cs", Case.Sensitive);

        HookPayloads.Stop("aborted").GetProperty("status").GetString().ShouldBe("aborted", Case.Sensitive);

        Names(HookPayloads.PreCompact("auto", 85, 120000, 128000, 45, 30, true)).ShouldBe(
            ["trigger", "context_usage_percent", "context_tokens", "context_window_size", "message_count", "messages_to_compact", "is_first_compaction"],
            ignoreOrder: false);
    }

    [Fact]
    public void AgentTextAndFilePayloadsCarryTheirDocumentedFields()
    {
        HookPayloads.AfterAgentResponse("done").GetProperty("text").GetString().ShouldBe("done", Case.Sensitive);
        Names(HookPayloads.AfterAgentThought("hmm", 5000)).ShouldBe(["text", "duration_ms"], ignoreOrder: false);
        Names(HookPayloads.AfterAgentThought("hmm")).ShouldBe(["text"], ignoreOrder: false);
        Names(HookPayloads.BeforeReadFile("/a.cs", "x", [])).ShouldBe(["file_path", "content", "attachments"], ignoreOrder: false);
        var edit = HookPayloads.AfterFileEdit("/a.cs", [new HookFileEdit("old", "new")]);
        edit.GetProperty("edits")[0].GetProperty("old_string").GetString().ShouldBe("old", Case.Sensitive);
        edit.GetProperty("edits")[0].GetProperty("new_string").GetString().ShouldBe("new", Case.Sensitive);
    }

    [Fact]
    public void ShellPayloadsCarryTheirDocumentedFields()
    {
        Names(HookPayloads.BeforeShellExecution("ls", "/w", false)).ShouldBe(["command", "cwd", "sandbox"], ignoreOrder: false);
        Names(HookPayloads.AfterShellExecution("ls", "out", 12, false)).ShouldBe(["command", "output", "duration", "sandbox"], ignoreOrder: false);
    }

    [Fact]
    public void WorkspaceOpenHasNoEventSpecificFields()
    {
        Names(HookPayloads.WorkspaceOpen()).ShouldBeEmpty();
    }
}

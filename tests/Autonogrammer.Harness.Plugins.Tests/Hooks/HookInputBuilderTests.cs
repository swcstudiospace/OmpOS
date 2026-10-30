using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.Plugins.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Plugins.Tests.Hooks;

/// <summary>
/// Verifies the JSON a hook receives on standard input.
/// </summary>
public sealed class HookInputBuilderTests
{
    private static JsonElement Build(string eventName, HookSession session, string payload, int? loopCount = null)
    {
        var bytes = HookInputBuilder.Build(eventName, session, HookFixtures.Json(payload), loopCount);
        return HookFixtures.Json(Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void CommonFieldsComeFirstAndAreCompleteForAConversationEvent()
    {
        var session = new HookSession("conv-9", ["/work/a", "/work/b"])
        {
            Model = "claude-x",
            ModelId = "claude-x-id",
            ModelParameters = [new KeyValuePair<string, string>("effort", "max")],
            UserEmail = "ada@example.com",
            TranscriptPath = "/t/transcript.jsonl",
            CursorVersion = "autonogrammer-1.2",
        };

        var input = Build("beforeShellExecution", session, """{ "command": "ls", "cwd": "/work/a", "sandbox": false }""");

        input.GetProperty("conversation_id").GetString().ShouldBe("conv-9");
        input.GetProperty("generation_id").GetString().ShouldBe(session.GenerationId);
        input.GetProperty("model").GetString().ShouldBe("claude-x");
        input.GetProperty("model_id").GetString().ShouldBe("claude-x-id");
        input.GetProperty("model_params")[0].GetProperty("id").GetString().ShouldBe("effort");
        input.GetProperty("model_params")[0].GetProperty("value").GetString().ShouldBe("max");
        input.GetProperty("hook_event_name").GetString().ShouldBe("beforeShellExecution");
        input.GetProperty("cursor_version").GetString().ShouldBe("autonogrammer-1.2");
        input.GetProperty("workspace_roots").EnumerateArray().Select(root => root.GetString()).ShouldBe(["/work/a", "/work/b"], ignoreOrder: false);
        input.GetProperty("user_email").GetString().ShouldBe("ada@example.com");
        input.GetProperty("transcript_path").GetString().ShouldBe("/t/transcript.jsonl");
        input.GetProperty("command").GetString().ShouldBe("ls");
        input.GetProperty("sandbox").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void UnknownEmailAndTranscriptAreExplicitNullsAndOptionalModelFieldsAreOmitted()
    {
        var input = Build("stop", HookFixtures.Session(), """{ "status": "completed" }""");

        input.GetProperty("user_email").ValueKind.ShouldBe(JsonValueKind.Null);
        input.GetProperty("transcript_path").ValueKind.ShouldBe(JsonValueKind.Null);
        input.TryGetProperty("model_id", out _).ShouldBeFalse();
        input.TryGetProperty("model_params", out _).ShouldBeFalse();
    }

    [Fact]
    public void WorkspaceOpenOmitsTheConversationFields()
    {
        var session = new HookSession("conv-1", ["/work/a"]) { UserEmail = "ada@example.com" };

        var input = Build("workspaceOpen", session, "{}");

        foreach (var absent in new[] { "conversation_id", "generation_id", "model", "model_id", "model_params", "session_id", "transcript_path" })
        {
            input.TryGetProperty(absent, out _).ShouldBeFalse(absent);
        }

        input.GetProperty("hook_event_name").GetString().ShouldBe("workspaceOpen");
        input.GetProperty("workspace_roots").GetArrayLength().ShouldBe(1);
        input.GetProperty("user_email").GetString().ShouldBe("ada@example.com");
    }

    [Fact]
    public void ThePayloadCannotOverrideACommonField()
    {
        var input = Build("preToolUse", HookFixtures.Session(), """{ "conversation_id": "forged", "hook_event_name": "other", "workspace_roots": [], "tool_name": "Shell" }""");

        input.GetProperty("conversation_id").GetString().ShouldBe("conv-1");
        input.GetProperty("hook_event_name").GetString().ShouldBe("preToolUse");
        input.GetProperty("workspace_roots").GetArrayLength().ShouldBe(1);
        input.GetProperty("tool_name").GetString().ShouldBe("Shell");
    }

    [Fact]
    public void LoopCountIsTheHostsValueNotThePayloads()
    {
        var input = Build("stop", HookFixtures.Session(), """{ "status": "completed", "loop_count": 99 }""", loopCount: 2);

        input.GetProperty("loop_count").GetInt32().ShouldBe(2);
    }

    [Fact]
    public void AHugeStringFieldIsCutAtACharacterBoundaryAndFlagged()
    {
        var text = new string('a', HookInputBuilder.MaxStringBytes - 1) + "é" + "tail";
        var payload = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal) { ["content"] = text }, ObjectContext.Default.DictionaryStringString);

        var input = Build("beforeReadFile", HookFixtures.Session(), payload);

        input.GetProperty("content_truncated").GetBoolean().ShouldBeTrue();
        var cut = input.GetProperty("content").GetString()!;
        cut.Length.ShouldBe(HookInputBuilder.MaxStringBytes - 1);
        Encoding.UTF8.GetByteCount(cut).ShouldBeLessThanOrEqualTo(HookInputBuilder.MaxStringBytes);
    }

    [Fact]
    public void ASmallStringFieldIsNotFlagged()
    {
        var input = Build("afterAgentResponse", HookFixtures.Session(), """{ "text": "hello" }""");

        input.GetProperty("text").GetString().ShouldBe("hello");
        input.TryGetProperty("text_truncated", out _).ShouldBeFalse();
    }

    [Fact]
    public void ANonObjectPayloadAddsNothing()
    {
        var input = Build("sessionEnd", HookFixtures.Session(), "[1,2]");

        input.GetProperty("hook_event_name").GetString().ShouldBe("sessionEnd");
        input.EnumerateObject().Count().ShouldBe(8);
    }
}

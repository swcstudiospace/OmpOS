using System.Text.Json;
using Autonogrammer.Harness.AgentLoop.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Hooks;

/// <summary>
/// Verifies what the handler receives for a request (names, defaults, the payload kept as sent) and what the extension receives
/// for a decision (snake_case names, nulls left out, JSON members kept as they are).
/// </summary>
public sealed class HookBridgeDecisionTests
{
    private static JsonElement Element(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task EveryRequestMemberReachesTheHandlerAndThePayloadIsKeptAsSent()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync(
            "{\"event\":\"preToolUse\",\"kind\":\"cursor\",\"toolCallId\":\"call_1\",\"sessionId\":\"omp-7\","
            + "\"payload\":{\"tool_name\":\"Shell\",\"tool_input\":{\"command\":\"ls -la\",\"n\":[1,2.5,null]},\"cwd\":\"/w\u00e9\"}}").ConfigureAwait(true);

        response.Status.ShouldBe(200);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Event.ShouldBe("preToolUse");
        request.Kind.ShouldBe("cursor");
        request.ToolCallId.ShouldBe("call_1");
        request.SessionId.ShouldBe("omp-7");
        request.Payload.GetProperty("tool_name").GetString().ShouldBe("Shell");
        request.Payload.GetProperty("tool_input").GetRawText().ShouldBe("{\"command\":\"ls -la\",\"n\":[1,2.5,null]}");
        request.Payload.GetProperty("cwd").GetString().ShouldBe("/w\u00e9");
    }

    [Fact]
    public async Task AMissingKindIsCursorAndAMissingPayloadIsAnEmptyObject()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync("{\"event\":\"stop\"}").ConfigureAwait(true);

        response.Status.ShouldBe(200);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Kind.ShouldBe("cursor");
        request.ToolCallId.ShouldBeNull();
        request.SessionId.ShouldBeNull();
        request.Payload.ValueKind.ShouldBe(JsonValueKind.Object);
        request.Payload.EnumerateObject().Any().ShouldBeFalse();
    }

    [Fact]
    public async Task AnOmpEventKeepsItsKind()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync("{\"event\":\"tool_call\",\"kind\":\"omp\",\"payload\":{\"toolName\":\"bash\"}}").ConfigureAwait(true);

        response.Status.ShouldBe(200);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Event.ShouldBe("tool_call");
        request.Kind.ShouldBe("omp");
        request.Payload.GetProperty("toolName").GetString().ShouldBe("bash");
    }

    [Fact]
    public async Task AnEmptyDecisionIsWrittenWithOnlyTheTwoNonNullableMembers()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision());
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync("{\"event\":\"stop\"}").ConfigureAwait(true);

        response.PropertyNames().ShouldBe(["hooks_run", "failed_open"], ignoreOrder: true);
        response.Json().GetProperty("hooks_run").GetInt32().ShouldBe(0);
        response.Json().GetProperty("failed_open").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task EveryDecisionMemberIsWrittenUnderItsSnakeCaseNameAndJsonMembersAreKeptAsTheyAre()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision
        {
            Permission = BridgeDecision.PermissionAllow,
            UserMessage = "for the human",
            AgentMessage = "for the model",
            UpdatedInput = Element("{\"command\":\"echo \\\"hi\\\"\",\"nested\":{\"list\":[1,2,3]}}"),
            AdditionalContext = "extra context",
            FollowupMessage = "keep going",
            Continue = false,
            UpdatedMcpToolOutput = Element("{\"content\":[{\"type\":\"text\",\"text\":\"ok\"}]}"),
            Env = new Dictionary<string, string>(StringComparer.Ordinal) { ["KEY"] = "value" },
            OmpResult = Element("{\"block\":false}"),
            HooksRun = 3,
            FailedOpen = false,
        });
        var rig = await HookBridgeRig.StartAsync(handler).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var response = await rig.PostAsync("{\"event\":\"preToolUse\"}").ConfigureAwait(true);

        response.PropertyNames().ShouldBe(
            [
                "permission",
                "user_message",
                "agent_message",
                "updated_input",
                "additional_context",
                "followup_message",
                "continue",
                "updated_mcp_tool_output",
                "env",
                "omp_result",
                "hooks_run",
                "failed_open",
            ],
            ignoreOrder: true);
        var json = response.Json();
        json.GetProperty("permission").GetString().ShouldBe("allow");
        json.GetProperty("user_message").GetString().ShouldBe("for the human");
        json.GetProperty("agent_message").GetString().ShouldBe("for the model");
        json.GetProperty("updated_input").GetProperty("command").GetString().ShouldBe("echo \"hi\"");
        json.GetProperty("updated_input").GetProperty("nested").GetProperty("list").GetArrayLength().ShouldBe(3);
        json.GetProperty("additional_context").GetString().ShouldBe("extra context");
        json.GetProperty("followup_message").GetString().ShouldBe("keep going");
        json.GetProperty("continue").GetBoolean().ShouldBeFalse();
        json.GetProperty("updated_mcp_tool_output").GetProperty("content")[0].GetProperty("text").GetString().ShouldBe("ok");
        json.GetProperty("env").GetProperty("KEY").GetString().ShouldBe("value");
        json.GetProperty("omp_result").GetProperty("block").GetBoolean().ShouldBeFalse();
        json.GetProperty("hooks_run").GetInt32().ShouldBe(3);
        json.GetProperty("failed_open").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task ADecisionWithAnUndefinedJsonMemberIsAnsweredByTheFailureRuleInsteadOfBreakingTheConnection()
    {
        var handler = FakeBridgeHandler.Returning(new BridgeDecision { Permission = BridgeDecision.PermissionAllow, UpdatedInput = default(JsonElement) });
        var options = new HookBridgeOptions { FailClosedEvents = new HashSet<string>(StringComparer.Ordinal) { "preToolUse" } };
        var rig = await HookBridgeRig.StartAsync(handler, options).ConfigureAwait(true);
        await using var lifetime = rig.ConfigureAwait(true);

        var closed = await rig.PostAsync("{\"event\":\"preToolUse\"}").ConfigureAwait(true);
        var open = await rig.PostAsync("{\"event\":\"postToolUse\"}").ConfigureAwait(true);

        closed.Status.ShouldBe(200);
        closed.Json().GetProperty("permission").GetString().ShouldBe("deny");
        open.Status.ShouldBe(200);
        open.Json().GetProperty("failed_open").GetBoolean().ShouldBeTrue();
        open.PropertyNames().ShouldNotContain("permission", StringComparer.Ordinal);
    }
}

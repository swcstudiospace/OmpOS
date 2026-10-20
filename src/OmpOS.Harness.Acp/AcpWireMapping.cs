using System.Collections.Frozen;
using System.Text.Json;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp.AcpProtocol;

namespace OmpOS.Harness.Acp;

/// <summary>Maps ACP wire payloads to harness events and capability flags.</summary>
internal static class AcpWireMapping
{
    /// <summary>
    /// Maps one <c>session/update</c> payload. Returns <see langword="null"/> for kinds the harness does not model
    /// (for example <c>user_message_chunk</c> or <c>available_commands_update</c>); ACP reserves room for new kinds.
    /// </summary>
    /// <param name="update">The raw update object.</param>
    /// <returns>The mapped event, or <see langword="null"/>.</returns>
    /// <exception cref="JsonException">The update does not have the shape of its declared kind.</exception>
    public static HarnessEvent? MapSessionUpdate(JsonElement update)
    {
        var header = update.Deserialize(AcpJsonContext.Default.SessionUpdateHeader);
        return header?.SessionUpdate switch
        {
            "agent_message_chunk" => TextOf(update) is { } text ? new TextDeltaEvent(text) : null,
            "agent_thought_chunk" => TextOf(update) is { } thought ? new ThoughtEvent(thought) : null,
            "tool_call" => ToolCall(update),
            "tool_call_update" => ToolCallUpdate(update),
            "plan" => Plan(update),
            _ => null,
        };
    }

    /// <summary>Flattens the agent capabilities into dotted keys. Boolean flags map to <c>true</c>/<c>false</c>; object capabilities map to <c>true</c> when present.</summary>
    /// <param name="capabilities">The capabilities reported in <c>initialize</c>.</param>
    /// <returns>The flattened capability dictionary.</returns>
    public static IReadOnlyDictionary<string, string> Flatten(AcpFrames.AgentCapabilities capabilities)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["loadSession"] = Flag(capabilities.LoadSession),
            ["mcpCapabilities.http"] = Flag(capabilities.McpCapabilities.Http),
            ["mcpCapabilities.sse"] = Flag(capabilities.McpCapabilities.Sse),
            ["promptCapabilities.embeddedContext"] = Flag(capabilities.PromptCapabilities.EmbeddedContext),
            ["promptCapabilities.image"] = Flag(capabilities.PromptCapabilities.Image),
        };

        var session = capabilities.SessionCapabilities;
        AddPresence(map, "sessionCapabilities.list", session.List);
        AddPresence(map, "sessionCapabilities.fork", session.Fork);
        AddPresence(map, "sessionCapabilities.resume", session.Resume);
        AddPresence(map, "sessionCapabilities.close", session.Close);
        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>Renders plan entries as a markdown checklist.</summary>
    /// <param name="entries">The plan entries.</param>
    /// <returns>One checklist line per entry.</returns>
    public static string RenderPlan(IEnumerable<AcpFrames.PlanEntry> entries) =>
        string.Join('\n', entries.Select(static entry => $"- {Box(entry.Status)} {entry.Content}"));

    private static string? TextOf(JsonElement update) =>
        update.Deserialize(AcpJsonContext.Default.ChunkUpdate) is { Content: { Type: "text", Text: { } text } } ? text : null;

    private static ToolCallEvent ToolCall(JsonElement update)
    {
        var call = update.Deserialize(AcpJsonContext.Default.ToolCallPayload) ?? new AcpFrames.ToolCallPayload();
        return new ToolCallEvent(call.ToolCallId, call.Title ?? call.Kind ?? "tool", update.GetRawText());
    }

    private static ToolCallUpdateEvent ToolCallUpdate(JsonElement update)
    {
        var call = update.Deserialize(AcpJsonContext.Default.ToolCallPayload) ?? new AcpFrames.ToolCallPayload();
        return new ToolCallUpdateEvent(call.ToolCallId, call.Status ?? string.Empty, update.GetRawText());
    }

    private static PlanEvent Plan(JsonElement update)
    {
        var plan = update.Deserialize(AcpJsonContext.Default.PlanUpdate);
        return new PlanEvent(RenderPlan(plan?.Entries ?? []));
    }

    private static string Box(string status) => status switch
    {
        "completed" => "[x]",
        "in_progress" => "[~]",
        _ => "[ ]",
    };

    private static string Flag(bool value) => value ? "true" : "false";

    private static void AddPresence(Dictionary<string, string> map, string key, JsonElement? capability)
    {
        if (capability is not null)
        {
            map[key] = "true";
        }
    }
}

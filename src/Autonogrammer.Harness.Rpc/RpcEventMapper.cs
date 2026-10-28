using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.Rpc;

/// <summary>
/// Maps omp agent-session stream frames onto harness events. Only frames the harness models are mapped; every other
/// frame yields null and is ignored.
/// </summary>
internal static class RpcEventMapper
{
    private const string InProgress = "in_progress";
    private const string Completed = "completed";
    private const string Failed = "failed";

    /// <summary>Maps one stream frame.</summary>
    /// <param name="type">The frame discriminator.</param>
    /// <param name="frame">The parsed frame.</param>
    /// <returns>The harness event, or null when the frame has no harness equivalent.</returns>
    public static HarnessEvent? Map(string type, JsonElement frame) => type switch
    {
        "message_update" => MapMessageUpdate(frame),
        "tool_execution_start" => MapToolStart(frame),
        "tool_execution_update" => MapToolUpdate(frame),
        "tool_execution_end" => MapToolEnd(frame),
        _ => null,
    };

    private static HarnessEvent? MapMessageUpdate(JsonElement frame)
    {
        if (!frame.TryGetProperty("assistantMessageEvent", out JsonElement inner)
            || !TryString(inner, "type", out string? kind)
            || !TryString(inner, "delta", out string? delta))
        {
            return null;
        }

        return kind switch
        {
            "text_delta" => new TextDeltaEvent(delta),
            "thinking_delta" => new ThoughtEvent(delta),
            _ => null,
        };
    }

    private static ToolCallEvent? MapToolStart(JsonElement frame) =>
        TryString(frame, "toolCallId", out string? callId) && TryString(frame, "toolName", out string? name)
            ? new ToolCallEvent(callId, name, frame.GetRawText())
            : null;

    private static ToolCallUpdateEvent? MapToolUpdate(JsonElement frame) =>
        TryString(frame, "toolCallId", out string? callId)
            ? new ToolCallUpdateEvent(callId, InProgress, frame.GetRawText())
            : null;

    private static ToolCallUpdateEvent? MapToolEnd(JsonElement frame)
    {
        if (!TryString(frame, "toolCallId", out string? callId))
        {
            return null;
        }

        bool isError = frame.TryGetProperty("isError", out JsonElement error) && error.ValueKind == JsonValueKind.True;
        return new ToolCallUpdateEvent(callId, isError ? Failed : Completed, frame.GetRawText());
    }

    private static bool TryString(JsonElement element, string property, [NotNullWhen(true)] out string? value)
    {
        if (element.TryGetProperty(property, out JsonElement found) && found.ValueKind == JsonValueKind.String)
        {
            value = found.GetString();
            return value is not null;
        }

        value = null;
        return false;
    }
}

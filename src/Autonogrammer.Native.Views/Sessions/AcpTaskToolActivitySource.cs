using System.Text.Json;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Presentation.Subagents;

namespace Autonogrammer.Native.Views.Sessions;

/// <summary>
/// The ACP activity source for the sub-agent panel. It reads <see cref="ToolCallEvent"/> and <see cref="ToolCallUpdateEvent"/>
/// values and ignores every other harness event. A call is a task when its title or kind is <c>task</c>. The agent name comes
/// from <c>rawInput</c> when that object carries one; otherwise the update leaves the name unset.
/// </summary>
public sealed class AcpTaskToolActivitySource : ISubagentActivitySource
{
    private readonly HashSet<string> _tasks = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public event EventHandler<SubagentActivityUpdateEventArgs>? Updated;

    /// <summary>Observes one harness event. Events that are not task tool calls are ignored.</summary>
    /// <param name="evt">The event from the ACP stream. Not null.</param>
    public void Observe(HarnessEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        switch (evt)
        {
            case ToolCallEvent call:
                ObserveFrame(call.CallId, call.Name, call.PayloadJson, statusText: null);
                break;
            case ToolCallUpdateEvent update:
                ObserveFrame(update.CallId, eventName: string.Empty, update.PayloadJson, update.Status);
                break;
        }
    }

    private void ObserveFrame(string callId, string eventName, string payloadJson, string? statusText)
    {
        if (string.IsNullOrWhiteSpace(callId) || string.IsNullOrWhiteSpace(payloadJson))
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payloadJson);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            var known = _tasks.Contains(callId);
            if (!known && !IsTask(eventName, root))
            {
                return;
            }

            _tasks.Add(callId);
            var status = string.IsNullOrWhiteSpace(statusText) ? StatusOf(root) : MapStatus(statusText);
            Updated?.Invoke(this, new SubagentActivityUpdateEventArgs(callId, AgentName(root), status, OutputLine(root)));
        }
    }

    private static bool IsTask(string eventName, JsonElement root) =>
        IsTaskToken(eventName) || IsTaskToken(StringOf(root, "title")) || IsTaskToken(StringOf(root, "kind"));

    private static bool IsTaskToken(string? value) =>
        string.Equals(value, "task", StringComparison.OrdinalIgnoreCase);

    private static string? AgentName(JsonElement root)
    {
        if (!root.TryGetProperty("rawInput", out var input) || input.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var key in new[] { "agent", "name", "subagent", "agentName", "agent_name" })
        {
            var text = StringOf(input, key);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }
        }

        return null;
    }

    private static string? OutputLine(JsonElement root)
    {
        if (root.TryGetProperty("rawOutput", out var raw))
        {
            var text = TextOf(raw);
            if (text is not null)
            {
                return LastLine(text);
            }
        }

        if (root.TryGetProperty("content", out var content))
        {
            var text = TextOf(content);
            if (text is not null)
            {
                return LastLine(text);
            }
        }

        return null;
    }

    private static string? TextOf(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return value.GetString();
            case JsonValueKind.Object:
                return StringOf(value, "text") ?? StringOf(value, "output");
            case JsonValueKind.Array:
                var parts = new List<string>();
                foreach (var item in value.EnumerateArray())
                {
                    var text = TextOf(item);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        parts.Add(text);
                    }
                }

                return parts.Count == 0 ? null : string.Join('\n', parts);
            default:
                return null;
        }
    }

    private static string LastLine(string text)
    {
        var last = string.Empty;
        foreach (var line in text.Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                last = line.Trim();
            }
        }

        return last;
    }

    private static SubagentStatus? StatusOf(JsonElement root) =>
        StringOf(root, "status") is { } status ? MapStatus(status) : SubagentStatus.Running;

    private static SubagentStatus MapStatus(string status)
    {
        if (Is(status, "completed") || Is(status, "done") || Is(status, "complete"))
        {
            return SubagentStatus.Done;
        }

        if (Is(status, "failed") || Is(status, "error"))
        {
            return SubagentStatus.Failed;
        }

        if (Is(status, "cancelled") || Is(status, "canceled"))
        {
            return SubagentStatus.Cancelled;
        }

        return SubagentStatus.Running;
    }

    private static bool Is(string status, string expected) =>
        string.Equals(status.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static string? StringOf(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

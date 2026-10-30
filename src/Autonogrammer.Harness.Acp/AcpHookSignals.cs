using System.Text;
using System.Text.Json;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// Turns the <c>session/update</c> traffic of one session into <see cref="AcpHookSignal"/> observations for the hook gate: tool calls and
/// their updates pass through, and the chunks of an assistant message or a thinking block are aggregated until the message ends (another
/// kind of update arrives, or the turn ends). Every method is non-blocking; the gate's <see cref="IAcpHookGate.Observe"/> only queues.
/// </summary>
internal sealed class AcpHookSignals(IAcpHookGate gate, TimeProvider clock)
{
    /// <summary>The most characters one aggregated message or output keeps. A longer text is cut; hooks get the head of it.</summary>
    internal const int MaxTextChars = 1024 * 1024;

    private readonly Lock _sync = new();
    private readonly StringBuilder _message = new();
    private readonly StringBuilder _thought = new();
    private long _thoughtStarted;
    private long _thoughtLast;

    /// <summary>Handles one update object of a <c>session/update</c> notification for the active turn.</summary>
    /// <param name="update">The update object. A shape the method does not understand is ignored.</param>
    internal void OnUpdate(JsonElement update)
    {
        if (update.ValueKind != JsonValueKind.Object || StringOf(update, "sessionUpdate") is not { } kind)
        {
            return;
        }

        switch (kind)
        {
            case "agent_message_chunk":
                AppendChunk(update, thought: false);
                break;
            case "agent_thought_chunk":
                AppendChunk(update, thought: true);
                break;
            case "tool_call":
                FlushText();
                gate.Observe(ToolCall(update, AcpHookSignalKind.ToolCall));
                break;
            case "tool_call_update":
                FlushText();
                gate.Observe(ToolCall(update, AcpHookSignalKind.ToolCallUpdate));
                break;
            case "plan":
                FlushText();
                gate.Observe(Plan(update));
                break;
            default:
                break;
        }
    }

    /// <summary>Reports the message and the thought that are still being aggregated. Called when the turn ends.</summary>
    internal void FlushText()
    {
        string? message = null;
        string? thought = null;
        TimeSpan? duration = null;
        lock (_sync)
        {
            if (_message.Length > 0)
            {
                message = _message.ToString();
                _message.Clear();
            }

            if (_thought.Length > 0)
            {
                thought = _thought.ToString();
                duration = clock.GetElapsedTime(_thoughtStarted, _thoughtLast);
                _thought.Clear();
            }
        }

        if (thought is not null)
        {
            gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.AgentThought, Text = thought, Duration = duration });
        }

        if (message is not null)
        {
            gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.AgentResponse, Text = message });
        }
    }

    private static AcpHookSignal ToolCall(JsonElement update, AcpHookSignalKind kind) => new()
    {
        Kind = kind,
        ToolCallId = StringOf(update, "toolCallId"),
        Title = StringOf(update, "title"),
        ToolKind = StringOf(update, "kind"),
        Status = StringOf(update, "status"),
        RawInput = update.TryGetProperty("rawInput", out var input) && input.ValueKind != JsonValueKind.Null ? input.Clone() : null,
        RawOutput = update.TryGetProperty("rawOutput", out var output) && output.ValueKind != JsonValueKind.Null ? output.Clone() : null,
        Text = kind == AcpHookSignalKind.ToolCallUpdate ? OutputText(update) : null,
    };

    private static AcpHookSignal Plan(JsonElement update)
    {
        var lines = new List<string>();
        if (update.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                var content = StringOf(entry, "content") ?? string.Empty;
                var box = StringOf(entry, "status") switch
                {
                    "completed" => "[x]",
                    "in_progress" => "[~]",
                    _ => "[ ]",
                };
                lines.Add("- " + box + " " + content);
            }
        }

        return new AcpHookSignal { Kind = AcpHookSignalKind.Plan, Text = string.Join('\n', lines) };
    }

    private static string? OutputText(JsonElement update)
    {
        var texts = new List<string>();
        if (update.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in content.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object
                    && string.Equals(StringOf(entry, "type"), "content", StringComparison.Ordinal)
                    && entry.TryGetProperty("content", out var inner)
                    && inner.ValueKind == JsonValueKind.Object
                    && string.Equals(StringOf(inner, "type"), "text", StringComparison.Ordinal)
                    && StringOf(inner, "text") is { Length: > 0 } text)
                {
                    texts.Add(text);
                }
            }
        }

        if (texts.Count == 0 && update.TryGetProperty("rawOutput", out var raw))
        {
            var rawText = raw.ValueKind switch
            {
                JsonValueKind.String => raw.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => raw.GetRawText(),
            };
            if (!string.IsNullOrEmpty(rawText))
            {
                texts.Add(rawText);
            }
        }

        if (texts.Count == 0)
        {
            return null;
        }

        var joined = string.Join('\n', texts);
        return joined.Length > MaxTextChars ? joined[..MaxTextChars] : joined;
    }

    private static string? StringOf(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private void AppendChunk(JsonElement update, bool thought)
    {
        if (!update.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.Object
            || !string.Equals(StringOf(content, "type"), "text", StringComparison.Ordinal)
            || StringOf(content, "text") is not { } text)
        {
            return;
        }

        // A chunk of the other stream ends the block that was being aggregated.
        string? flushedMessage = null;
        string? flushedThought = null;
        TimeSpan? flushedDuration = null;
        lock (_sync)
        {
            if (thought && _message.Length > 0)
            {
                flushedMessage = _message.ToString();
                _message.Clear();
            }
            else if (!thought && _thought.Length > 0)
            {
                flushedThought = _thought.ToString();
                flushedDuration = clock.GetElapsedTime(_thoughtStarted, _thoughtLast);
                _thought.Clear();
            }

            if (thought)
            {
                var now = clock.GetTimestamp();
                if (_thought.Length == 0)
                {
                    _thoughtStarted = now;
                }

                _thoughtLast = now;
                Append(_thought, text);
            }
            else
            {
                Append(_message, text);
            }
        }

        if (flushedThought is not null)
        {
            gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.AgentThought, Text = flushedThought, Duration = flushedDuration });
        }

        if (flushedMessage is not null)
        {
            gate.Observe(new AcpHookSignal { Kind = AcpHookSignalKind.AgentResponse, Text = flushedMessage });
        }
    }

    private static void Append(StringBuilder builder, string text)
    {
        var room = MaxTextChars - builder.Length;
        if (room <= 0)
        {
            return;
        }

        builder.Append(text, 0, Math.Min(room, text.Length));
    }
}

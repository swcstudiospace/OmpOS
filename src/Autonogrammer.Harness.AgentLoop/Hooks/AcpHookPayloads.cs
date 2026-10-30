using System.Buffers;
using System.Text.Json;
using Autonogrammer.Harness.Plugins.Hooks;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// Builds the event payloads the ACP tap produces for the tool events (<c>preToolUse</c>, <c>postToolUse</c>, <c>postToolUseFailure</c>), which
/// <see cref="HookPayloads"/> does not cover because the omp extension builds them, and maps ACP tool kinds to Cursor's tool types.
/// </summary>
internal static class AcpHookPayloads
{
    /// <summary>The most characters of a file's text one <c>afterFileEdit</c> edit carries.</summary>
    internal const int MaxEditChars = 64 * 1024;

    /// <summary>Maps an ACP tool kind (and, for MCP tools, the title) to the tool type Cursor's matchers use.</summary>
    /// <param name="kind">The ACP tool kind, or null.</param>
    /// <param name="title">The tool call title, or null.</param>
    /// <returns>The Cursor tool type: <c>Shell</c>, <c>Read</c>, <c>Write</c>, <c>Delete</c>, <c>Grep</c>, <c>WebSearch</c>, <c>Task</c>, <c>MCP:&lt;tool&gt;</c> or the kind itself.</returns>
    internal static string ToolName(string? kind, string? title)
    {
        if (title is { } name && name.StartsWith("mcp__", StringComparison.Ordinal))
        {
            var separator = name.IndexOf('_', "mcp__".Length);
            return "MCP:" + (separator > 0 ? name[(separator + 1)..] : name["mcp__".Length..]);
        }

        return kind switch
        {
            "execute" => "Shell",
            "read" => "Read",
            "edit" or "move" => "Write",
            "delete" => "Delete",
            "search" => "Grep",
            "fetch" => "WebSearch",
            "think" => "Task",
            null or "" => string.IsNullOrEmpty(title) ? "Task" : title,
            _ => kind,
        };
    }

    /// <summary>Returns the shell command of an <c>execute</c> tool call from its raw input, or the title when the input names none.</summary>
    /// <param name="rawInput">The raw input of the call, or null.</param>
    /// <param name="title">The tool call title, or null.</param>
    /// <returns>The command text.</returns>
    internal static string Command(JsonElement? rawInput, string? title)
    {
        if (rawInput is { ValueKind: JsonValueKind.Object } input
            && input.TryGetProperty("command", out var command)
            && command.ValueKind == JsonValueKind.String
            && command.GetString() is { Length: > 0 } text)
        {
            return text;
        }

        return title ?? string.Empty;
    }

    /// <summary>Builds the <c>preToolUse</c> payload.</summary>
    /// <param name="toolName">The Cursor tool type.</param>
    /// <param name="rawInput">The raw tool input, or null for an empty object.</param>
    /// <param name="toolUseId">The tool call identifier.</param>
    /// <param name="cwd">The working directory.</param>
    /// <returns>The payload.</returns>
    internal static JsonElement PreToolUse(string toolName, JsonElement? rawInput, string toolUseId, string cwd) => Build(writer =>
    {
        writer.WriteString("tool_name", toolName);
        WriteInput(writer, rawInput);
        writer.WriteString("tool_use_id", toolUseId);
        writer.WriteString("cwd", cwd);
    });

    /// <summary>Builds the <c>postToolUse</c> payload.</summary>
    /// <param name="toolName">The Cursor tool type.</param>
    /// <param name="rawInput">The raw tool input, or null for an empty object.</param>
    /// <param name="output">The output text, or null.</param>
    /// <param name="toolUseId">The tool call identifier.</param>
    /// <param name="cwd">The working directory.</param>
    /// <param name="duration">How long the call took.</param>
    /// <returns>The payload.</returns>
    internal static JsonElement PostToolUse(string toolName, JsonElement? rawInput, string? output, string toolUseId, string cwd, TimeSpan duration) => Build(writer =>
    {
        writer.WriteString("tool_name", toolName);
        WriteInput(writer, rawInput);
        writer.WriteString("tool_output", ToolOutputJson(output, isError: false));
        writer.WriteString("tool_use_id", toolUseId);
        writer.WriteString("cwd", cwd);
        writer.WriteNumber("duration", (long)duration.TotalMilliseconds);
    });

    /// <summary>Builds the <c>postToolUseFailure</c> payload.</summary>
    /// <param name="toolName">The Cursor tool type.</param>
    /// <param name="rawInput">The raw tool input, or null for an empty object.</param>
    /// <param name="error">The error text, or null.</param>
    /// <param name="toolUseId">The tool call identifier.</param>
    /// <param name="cwd">The working directory.</param>
    /// <param name="duration">How long the call took.</param>
    /// <returns>The payload.</returns>
    internal static JsonElement PostToolUseFailure(string toolName, JsonElement? rawInput, string? error, string toolUseId, string cwd, TimeSpan duration) => Build(writer =>
    {
        writer.WriteString("tool_name", toolName);
        WriteInput(writer, rawInput);
        writer.WriteString("tool_use_id", toolUseId);
        writer.WriteString("cwd", cwd);
        writer.WriteString("error_message", error ?? string.Empty);
        writer.WriteString("failure_type", "error");
        writer.WriteBoolean("is_interrupt", value: false);
        writer.WriteNumber("duration", (long)duration.TotalMilliseconds);
    });

    /// <summary>
    /// Describes a whole-file write as the edits of <c>afterFileEdit</c>: one edit holding what differs between the previous and the new text,
    /// with the common head and tail removed. A file that did not exist is one edit from the empty string. Identical texts give no edit.
    /// </summary>
    /// <param name="previous">The text before the write, or null when the file did not exist or was too large to compare.</param>
    /// <param name="content">The text that was written.</param>
    /// <returns>The edits.</returns>
    internal static IReadOnlyList<HookFileEdit> Edits(string? previous, string content)
    {
        if (previous is null)
        {
            return [new HookFileEdit(string.Empty, Cut(content))];
        }

        if (string.Equals(previous, content, StringComparison.Ordinal))
        {
            return [];
        }

        var head = 0;
        var limit = Math.Min(previous.Length, content.Length);
        while (head < limit && previous[head] == content[head])
        {
            head++;
        }

        var tail = 0;
        while (tail < limit - head && previous[previous.Length - 1 - tail] == content[content.Length - 1 - tail])
        {
            tail++;
        }

        return [new HookFileEdit(Cut(previous[head..(previous.Length - tail)]), Cut(content[head..(content.Length - tail)]))];
    }

    private static string Cut(string text) => text.Length > MaxEditChars ? text[..MaxEditChars] : text;

    private static string ToolOutputJson(string? output, bool isError)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("content");
            if (!string.IsNullOrEmpty(output))
            {
                writer.WriteStringValue(output);
            }

            writer.WriteEndArray();
            writer.WriteBoolean("isError", isError);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteInput(Utf8JsonWriter writer, JsonElement? rawInput)
    {
        writer.WritePropertyName("tool_input");
        if (rawInput is { ValueKind: JsonValueKind.Object } input)
        {
            input.WriteTo(writer);
        }
        else
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
        }
    }

    private static JsonElement Build(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}

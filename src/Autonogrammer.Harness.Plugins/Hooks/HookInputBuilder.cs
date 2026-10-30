using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Builds the JSON a hook receives on standard input: the common fields every Cursor hook gets
/// (<c>conversation_id</c>, <c>generation_id</c>, <c>model</c>, <c>hook_event_name</c>, <c>cursor_version</c>,
/// <c>workspace_roots</c>, <c>user_email</c>, <c>transcript_path</c>) followed by the event's own fields. The event's fields
/// never override a common field. A top-level string longer than <see cref="MaxStringBytes"/> is cut at a character
/// boundary and flagged with a sibling <c>&lt;name&gt;_truncated: true</c> field, so a huge file or command output cannot
/// exceed the input cap. <c>workspaceOpen</c> runs outside any conversation and omits the conversation fields.
/// </summary>
public static class HookInputBuilder
{
    /// <summary>The longest top-level string field that is passed whole, 1 MiB of UTF-8.</summary>
    public const int MaxStringBytes = 1024 * 1024;

    /// <summary>
    /// Builds the input JSON for one hook.
    /// </summary>
    /// <param name="eventName">The value of <c>hook_event_name</c>: a Cursor event name or an omp extension event name.</param>
    /// <param name="session">The conversation the event belongs to.</param>
    /// <param name="payload">The event-specific fields as a JSON object; any other kind of value adds nothing.</param>
    /// <param name="loopCount">The <c>loop_count</c> to send for <c>stop</c> and <c>subagentStop</c> hooks, or null to keep the payload's value.</param>
    /// <returns>UTF-8 JSON, without a trailing newline.</returns>
    public static byte[] Build(string eventName, HookSession session, JsonElement payload, int? loopCount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(session);
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            var written = WriteCommonFields(writer, eventName, session);
            if (loopCount is { } count)
            {
                writer.WriteNumber("loop_count", count);
                written.Add("loop_count");
            }

            if (payload.ValueKind == JsonValueKind.Object)
            {
                WritePayload(writer, payload, written);
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    private static HashSet<string> WriteCommonFields(Utf8JsonWriter writer, string eventName, HookSession session)
    {
        var written = new HashSet<string>(StringComparer.Ordinal);
        var conversational = !string.Equals(eventName, HookEvents.Format(HookEvent.WorkspaceOpen), StringComparison.Ordinal);
        if (conversational)
        {
            Write(writer, written, "conversation_id", session.ConversationId);
            Write(writer, written, "generation_id", session.GenerationId);
            Write(writer, written, "model", session.Model);
            if (session.ModelId is not null)
            {
                Write(writer, written, "model_id", session.ModelId);
            }

            if (session.ModelParameters.Count > 0)
            {
                writer.WriteStartArray("model_params");
                foreach (var parameter in session.ModelParameters)
                {
                    writer.WriteStartObject();
                    writer.WriteString("id", parameter.Key);
                    writer.WriteString("value", parameter.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                written.Add("model_params");
            }
        }

        Write(writer, written, "hook_event_name", eventName);
        Write(writer, written, "cursor_version", session.CursorVersion);
        writer.WriteStartArray("workspace_roots");
        foreach (var root in session.WorkspaceRoots)
        {
            writer.WriteStringValue(root);
        }

        writer.WriteEndArray();
        written.Add("workspace_roots");
        WriteNullable(writer, written, "user_email", session.UserEmail);
        if (conversational)
        {
            WriteNullable(writer, written, "transcript_path", session.TranscriptPath);
        }

        return written;
    }

    private static void WritePayload(Utf8JsonWriter writer, JsonElement payload, HashSet<string> written)
    {
        foreach (var property in payload.EnumerateObject())
        {
            if (written.Contains(property.Name))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() is { } text
                && Encoding.UTF8.GetByteCount(text) > MaxStringBytes)
            {
                writer.WriteString(property.Name, CutUtf8(text, MaxStringBytes));
                writer.WriteBoolean(property.Name + "_truncated", true);
                continue;
            }

            writer.WritePropertyName(property.Name);
            property.Value.WriteTo(writer);
        }
    }

    private static void Write(Utf8JsonWriter writer, HashSet<string> written, string name, string value)
    {
        writer.WriteString(name, value);
        written.Add(name);
    }

    private static void WriteNullable(Utf8JsonWriter writer, HashSet<string> written, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }

        written.Add(name);
    }

    private static string CutUtf8(string text, int maxBytes)
    {
        var bytes = 0;
        var end = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > maxBytes)
            {
                break;
            }

            bytes += rune.Utf8SequenceLength;
            end += rune.Utf16SequenceLength;
        }

        return text[..end];
    }
}

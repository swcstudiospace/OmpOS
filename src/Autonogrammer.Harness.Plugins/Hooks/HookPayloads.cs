using System.Buffers;
using System.Text.Json;

namespace Autonogrammer.Harness.Plugins.Hooks;

/// <summary>
/// Builds the event-specific payloads that the host itself produces (events that no omp extension reports), with the field
/// names of https://cursor.com/docs/hooks. The common fields are added by <see cref="HookInputBuilder"/>.
/// </summary>
public static class HookPayloads
{
    /// <summary>The payload of <c>sessionStart</c>.</summary>
    /// <param name="sessionId">The session id, the same as <c>conversation_id</c>.</param>
    /// <param name="isBackgroundAgent">True for a background agent session.</param>
    /// <param name="composerMode">The mode the conversation starts in, such as <c>agent</c>, <c>ask</c> or <c>edit</c>.</param>
    public static JsonElement SessionStart(string sessionId, bool isBackgroundAgent = false, string? composerMode = "agent") => Build(writer =>
    {
        writer.WriteString("session_id", sessionId);
        writer.WriteBoolean("is_background_agent", isBackgroundAgent);
        if (composerMode is not null)
        {
            writer.WriteString("composer_mode", composerMode);
        }
    });

    /// <summary>The payload of <c>sessionEnd</c>.</summary>
    /// <param name="sessionId">The session id.</param>
    /// <param name="reason">One of <c>completed</c>, <c>aborted</c>, <c>error</c>, <c>window_close</c>, <c>user_close</c>.</param>
    /// <param name="durationMs">The session length in milliseconds.</param>
    /// <param name="isBackgroundAgent">True for a background agent session.</param>
    /// <param name="finalStatus">The final status string.</param>
    /// <param name="errorMessage">The error details when <paramref name="reason"/> is <c>error</c>.</param>
    public static JsonElement SessionEnd(string sessionId, string reason, long durationMs, bool isBackgroundAgent, string finalStatus, string? errorMessage = null) => Build(writer =>
    {
        writer.WriteString("session_id", sessionId);
        writer.WriteString("reason", reason);
        writer.WriteNumber("duration_ms", durationMs);
        writer.WriteBoolean("is_background_agent", isBackgroundAgent);
        writer.WriteString("final_status", finalStatus);
        if (errorMessage is not null)
        {
            writer.WriteString("error_message", errorMessage);
        }
    });

    /// <summary>The payload of <c>beforeSubmitPrompt</c>.</summary>
    /// <param name="prompt">The user's prompt text.</param>
    /// <param name="attachments">The attachments of the prompt.</param>
    public static JsonElement BeforeSubmitPrompt(string prompt, IReadOnlyList<HookAttachment> attachments) => Build(writer =>
    {
        writer.WriteString("prompt", prompt);
        WriteAttachments(writer, attachments);
    });

    /// <summary>The payload of <c>stop</c>.</summary>
    /// <param name="status">One of <c>completed</c>, <c>aborted</c>, <c>error</c>.</param>
    public static JsonElement Stop(string status) => Build(writer => writer.WriteString("status", status));

    /// <summary>The payload of <c>preCompact</c>.</summary>
    /// <param name="trigger"><c>auto</c> or <c>manual</c>.</param>
    /// <param name="contextUsagePercent">The context window usage, 0 to 100.</param>
    /// <param name="contextTokens">The current context size in tokens.</param>
    /// <param name="contextWindowSize">The context window size in tokens.</param>
    /// <param name="messageCount">The number of messages in the conversation.</param>
    /// <param name="messagesToCompact">The number of messages that will be summarized.</param>
    /// <param name="isFirstCompaction">True when this is the conversation's first compaction.</param>
    public static JsonElement PreCompact(string trigger, int contextUsagePercent, long contextTokens, long contextWindowSize, int messageCount, int messagesToCompact, bool isFirstCompaction) => Build(writer =>
    {
        writer.WriteString("trigger", trigger);
        writer.WriteNumber("context_usage_percent", contextUsagePercent);
        writer.WriteNumber("context_tokens", contextTokens);
        writer.WriteNumber("context_window_size", contextWindowSize);
        writer.WriteNumber("message_count", messageCount);
        writer.WriteNumber("messages_to_compact", messagesToCompact);
        writer.WriteBoolean("is_first_compaction", isFirstCompaction);
    });

    /// <summary>The payload of <c>afterAgentResponse</c>.</summary>
    /// <param name="text">The final assistant text.</param>
    public static JsonElement AfterAgentResponse(string text) => Build(writer => writer.WriteString("text", text));

    /// <summary>The payload of <c>afterAgentThought</c>.</summary>
    /// <param name="text">The fully aggregated thinking text.</param>
    /// <param name="durationMs">How long the thinking block took, when known.</param>
    public static JsonElement AfterAgentThought(string text, long? durationMs = null) => Build(writer =>
    {
        writer.WriteString("text", text);
        if (durationMs is { } duration)
        {
            writer.WriteNumber("duration_ms", duration);
        }
    });

    /// <summary>The payload of <c>beforeReadFile</c>.</summary>
    /// <param name="filePath">The absolute path being read.</param>
    /// <param name="content">The file's contents.</param>
    /// <param name="attachments">The attachments of the prompt.</param>
    public static JsonElement BeforeReadFile(string filePath, string content, IReadOnlyList<HookAttachment> attachments) => Build(writer =>
    {
        writer.WriteString("file_path", filePath);
        writer.WriteString("content", content);
        WriteAttachments(writer, attachments);
    });

    /// <summary>The payload of <c>afterFileEdit</c>.</summary>
    /// <param name="filePath">The absolute path of the edited file.</param>
    /// <param name="edits">The edits that were applied.</param>
    public static JsonElement AfterFileEdit(string filePath, IReadOnlyList<HookFileEdit> edits) => Build(writer =>
    {
        writer.WriteString("file_path", filePath);
        writer.WriteStartArray("edits");
        foreach (var edit in edits)
        {
            writer.WriteStartObject();
            writer.WriteString("old_string", edit.OldString);
            writer.WriteString("new_string", edit.NewString);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    });

    /// <summary>The payload of <c>beforeShellExecution</c>.</summary>
    /// <param name="command">The full terminal command.</param>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <param name="sandbox">True when the command runs sandboxed.</param>
    public static JsonElement BeforeShellExecution(string command, string workingDirectory, bool sandbox) => Build(writer =>
    {
        writer.WriteString("command", command);
        writer.WriteString("cwd", workingDirectory);
        writer.WriteBoolean("sandbox", sandbox);
    });

    /// <summary>The payload of <c>afterShellExecution</c>.</summary>
    /// <param name="command">The full terminal command.</param>
    /// <param name="output">The full terminal output.</param>
    /// <param name="durationMs">The execution time in milliseconds, without approval wait.</param>
    /// <param name="sandbox">True when the command ran sandboxed.</param>
    public static JsonElement AfterShellExecution(string command, string output, long durationMs, bool sandbox) => Build(writer =>
    {
        writer.WriteString("command", command);
        writer.WriteString("output", output);
        writer.WriteNumber("duration", durationMs);
        writer.WriteBoolean("sandbox", sandbox);
    });

    /// <summary>The payload of <c>workspaceOpen</c>: no event-specific fields.</summary>
    public static JsonElement WorkspaceOpen() => Build(_ => { });

    private static void WriteAttachments(Utf8JsonWriter writer, IReadOnlyList<HookAttachment> attachments)
    {
        writer.WriteStartArray("attachments");
        foreach (var attachment in attachments)
        {
            writer.WriteStartObject();
            writer.WriteString("type", attachment.Type);
            writer.WriteString("file_path", attachment.FilePath);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static JsonElement Build(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
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

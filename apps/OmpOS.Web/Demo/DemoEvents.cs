using System.Text.Json;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Controls;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;

namespace OmpOS.Web.Demo;

/// <summary>Builds the stream payloads the Demo scripts emit, in the exact shapes the real API stores.</summary>
internal static class DemoEvents
{
    /// <summary>The event kind of a prompt the API accepted.</summary>
    internal const string PromptAccepted = "prompt_accepted";

    /// <summary>The event kind of a requested tool approval.</summary>
    internal const string ToolApprovalRequested = "tool_approval_requested";

    /// <summary>The event kind of an approved tool request.</summary>
    internal const string ToolApprovalApproved = "tool_approval_approved";

    /// <summary>The event kind of a denied tool request.</summary>
    internal const string ToolApprovalDenied = "tool_approval_denied";

    /// <summary>The event kind of a steering message.</summary>
    internal const string Steer = "steer";

    /// <summary>The event kind of a queued follow-up.</summary>
    internal const string FollowUp = "follow_up";

    /// <summary>Gets the SSE event name of a payload, as the real API names it.</summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The event name.</returns>
    internal static string KindOf(StreamEventPayload payload) => payload switch
    {
        TextDeltaPayload => "text_delta",
        UiBlockPayload => "ui_block",
        ErrorPayload => "error",
        DiagnosticPayload => "diagnostic",
        UserMessagePayload => "user_message",
        CompletedPayload => "completed",
        UnknownPayload { OriginalKind: { } kind } => kind,
        _ => "unknown",
    };

    /// <summary>Gets whether a payload ends a turn: the stream of a session closes after it.</summary>
    /// <param name="payload">The payload.</param>
    /// <returns>True for a completed or error payload.</returns>
    internal static bool IsTerminal(StreamEventPayload payload) => payload is CompletedPayload or ErrorPayload;

    /// <summary>Builds the payload that shows the user's prompt.</summary>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>The payload.</returns>
    internal static UnknownPayload Prompt(string prompt) => Unknown(PromptAccepted, new Dictionary<string, string>(StringComparer.Ordinal) { ["prompt"] = prompt });

    /// <summary>Builds a streamed text delta.</summary>
    /// <param name="delta">The text.</param>
    /// <returns>The payload.</returns>
    internal static TextDeltaPayload Delta(string delta) => new() { Delta = delta };

    /// <summary>Builds a UI block payload.</summary>
    /// <param name="block">The block.</param>
    /// <returns>The payload.</returns>
    internal static UiBlockPayload Block(UiBlock block) => new() { Block = block };

    /// <summary>Builds the completed payload.</summary>
    /// <returns>The payload.</returns>
    internal static CompletedPayload Completed() => new() { Reason = "completed" };

    /// <summary>Builds an error payload.</summary>
    /// <param name="message">The message.</param>
    /// <param name="retryable">Whether the user may retry.</param>
    /// <returns>The payload.</returns>
    internal static ErrorPayload Error(string message, bool retryable) => new() { Message = message, Retryable = retryable };

    /// <summary>Builds the payload of a requested tool approval.</summary>
    /// <param name="requestId">The request.</param>
    /// <param name="toolName">The tool.</param>
    /// <returns>The payload.</returns>
    internal static UnknownPayload ApprovalRequested(string requestId, string toolName) =>
        Unknown(ToolApprovalRequested, new Dictionary<string, string>(StringComparer.Ordinal) { ["requestId"] = requestId, ["toolName"] = toolName });

    /// <summary>Builds the payload of a decided tool approval.</summary>
    /// <param name="requestId">The request.</param>
    /// <param name="allow">True for approved.</param>
    /// <returns>The payload.</returns>
    internal static UnknownPayload ApprovalDecided(string requestId, bool allow) =>
        Unknown(allow ? ToolApprovalApproved : ToolApprovalDenied, new Dictionary<string, string>(StringComparer.Ordinal) { ["requestId"] = requestId });

    /// <summary>Builds the payload of a steering message or a queued follow-up. It carries the text length, never the text.</summary>
    /// <param name="kind">The event kind.</param>
    /// <param name="length">The length of the message.</param>
    /// <returns>The payload.</returns>
    internal static UnknownPayload Steering(string kind, int length) =>
        Unknown(kind, new Dictionary<string, string>(StringComparer.Ordinal) { ["length"] = length.ToString(System.Globalization.CultureInfo.InvariantCulture) });

    /// <summary>Builds the payload of a stage event.</summary>
    /// <param name="stage">The stage.</param>
    /// <returns>The payload.</returns>
    internal static UnknownPayload Stage(StageEvent stage) => StreamEventPayloads.CreateStage(stage);

    /// <summary>Builds the payload of a changed option.</summary>
    /// <param name="optionId">The option.</param>
    /// <param name="value">The new value.</param>
    /// <returns>The payload.</returns>
    internal static UnknownPayload OptionChanged(string optionId, string value) =>
        StreamEventPayloads.CreateConfigOptionUpdate(new ConfigOptionUpdateEvent { OptionId = optionId, CurrentValue = value });

    /// <summary>Parses JSON text to a detached element.</summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The element.</returns>
    internal static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static UnknownPayload Unknown(string kind, Dictionary<string, string> members)
    {
        var all = new Dictionary<string, string>(StringComparer.Ordinal) { ["kind"] = kind };
        foreach (var (name, value) in members)
        {
            all[name] = value;
        }

        return new UnknownPayload { OriginalKind = kind, Raw = Json(JsonSerializer.Serialize(all, DemoJson.Default.DictionaryStringString)) };
    }
}

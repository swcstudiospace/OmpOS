using OmpOS.Contracts.Streaming;

namespace OmpOS.Api.Sse;

/// <summary>
/// SSE event-type names for stream payloads: the payload kind, used as the <c>event:</c> field.
/// </summary>
internal static class StreamEventKind
{
    /// <summary>Returns the SSE event type for a payload.</summary>
    /// <param name="payload">The stream payload.</param>
    /// <returns>The event-type name.</returns>
    public static string Of(StreamEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload switch
        {
            TextDeltaPayload => "text_delta",
            UiBlockPayload => "ui_block",
            ErrorPayload => "error",
            CompletedPayload => "completed",
            UserMessagePayload => "user_message",
            DiagnosticPayload => "diagnostic",
            UnknownPayload unknown => unknown.OriginalKind ?? "unknown",
            _ => "unknown",
        };
    }

    /// <summary>Returns whether the payload terminates the stream.</summary>
    /// <param name="payload">The stream payload.</param>
    /// <returns>True for completed and error payloads.</returns>
    public static bool IsTerminal(StreamEventPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return payload is CompletedPayload or ErrorPayload;
    }
}

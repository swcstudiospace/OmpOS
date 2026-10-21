using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;

namespace OmpOS.App.Components;

/// <summary>
/// Client-side mirror of the API harness-event mapping: folds <see cref="HarnessEvent"/> values
/// from <c>IEngineHost</c> into session-log <see cref="StreamEvent"/> values the shared
/// <c>SessionViewModel</c> understands. Seq values are client-assigned display ordinals; the
/// engine session itself lives inside the host.
/// </summary>
internal static class EngineEventMapper
{
    /// <summary>Wraps one harness event as a session-log event with a client-assigned Seq.</summary>
    /// <param name="evt">The harness event.</param>
    /// <param name="sessionId">The display correlation identifier for this conversation.</param>
    /// <param name="seq">The client-assigned sequence ordinal.</param>
    /// <returns>The session-log event.</returns>
    internal static StreamEvent ToStreamEvent(HarnessEvent evt, string sessionId, long seq)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return new StreamEvent
        {
            Seq = seq,
            SessionId = sessionId,
            CorrelationId = sessionId,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = ToPayload(evt),
        };
    }

    /// <summary>Maps one harness event onto its log payload.</summary>
    /// <param name="evt">The harness event.</param>
    /// <returns>The equivalent stream payload.</returns>
    internal static StreamEventPayload ToPayload(HarnessEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        return evt switch
        {
            TextDeltaEvent text => new TextDeltaPayload { Delta = text.Text },
            UiBlockEvent block => new UiBlockPayload { Block = block.Block },
            ErrorEvent error => new ErrorPayload { Message = error.Message, Retryable = error.Retryable },
            CompletedEvent completed => new CompletedPayload { Reason = completed.Reason },
            ThoughtEvent thought => Unknown("thought", ("text", thought.Text)),
            ToolCallEvent call => Unknown("tool_call", ("callId", call.CallId), ("name", call.Name), ("payloadJson", call.PayloadJson)),
            ToolCallUpdateEvent update => Unknown("tool_call_update", ("callId", update.CallId), ("status", update.Status), ("payloadJson", update.PayloadJson)),
            PermissionRequestEvent permission => Unknown("permission_request", ("callId", permission.CallId), ("name", permission.Name), ("payloadJson", permission.PayloadJson)),
            PlanEvent plan => Unknown("plan", ("markdown", plan.Markdown)),
            _ => Unknown("unknown", ("type", evt.GetType().Name)),
        };
    }

    private static UnknownPayload Unknown(string kind, params (string Name, string Value)[] fields)
    {
        var obj = new JsonObject { ["kind"] = kind };
        foreach (var (name, value) in fields)
        {
            obj[name] = value;
        }

        using var doc = JsonDocument.Parse(obj.ToJsonString());
        return new UnknownPayload { OriginalKind = kind, Raw = doc.RootElement.Clone() };
    }
}

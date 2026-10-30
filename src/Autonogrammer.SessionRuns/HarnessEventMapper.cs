using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Sessions;

namespace Autonogrammer.SessionRuns;

/// <summary>
/// Maps harness stream events onto session-log payloads. Mirrors the session projection's
/// shape without referencing its internals: text, blocks, errors, and completions carry
/// over directly; engine-specific events degrade to unknown-kind payloads.
/// </summary>
internal static class HarnessEventMapper
{
    /// <summary>Maps one harness event onto its log payload.</summary>
    /// <param name="evt">The harness event.</param>
    /// <returns>The equivalent stream payload.</returns>
    public static StreamEventPayload ToPayload(HarnessEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        return evt switch
        {
            TextDeltaEvent text => new TextDeltaPayload { Delta = text.Text },
            UiBlockEvent block => new UiBlockPayload { Block = block.Block },
            ErrorEvent error => new ErrorPayload { Message = string.IsNullOrWhiteSpace(error.Message) ? SessionService.FailedTurnMessage : error.Message, Retryable = error.Retryable },
            CompletedEvent completed => new CompletedPayload { Reason = completed.Reason },
            DiagnosticEvent diagnostic => new DiagnosticPayload { Message = diagnostic.Message },
            ThoughtEvent thought => Unknown("thought", ("text", thought.Text)),
            ToolCallEvent call => Unknown("tool_call", ("callId", call.CallId), ("name", call.Name), ("payloadJson", call.PayloadJson)),
            ToolCallUpdateEvent update => Unknown("tool_call_update", ("callId", update.CallId), ("status", update.Status), ("payloadJson", update.PayloadJson)),
            PermissionRequestEvent permission => Unknown("permission_request", ("callId", permission.CallId), ("name", permission.Name), ("payloadJson", permission.PayloadJson)),
            PlanEvent plan => Unknown("plan", ("markdown", plan.Markdown)),
            ContextUsageEvent usage => Unknown("context_usage", ("used", usage.Used.ToString(CultureInfo.InvariantCulture)), ("size", usage.Size.ToString(CultureInfo.InvariantCulture))),
            _ => Unknown(evt.GetType().Name, ("type", evt.GetType().FullName ?? "unknown")),
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

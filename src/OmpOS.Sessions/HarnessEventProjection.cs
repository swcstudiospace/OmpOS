using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Sessions;

/// <summary>
/// The single pure projection from harness events to stream events and UI state (SESS-03).
/// </summary>
/// <remarks>
/// <para>
/// Nothing in this type reads the clock or any other ambient state: timestamps and sequence numbers are
/// arguments. Folding the same events from the same state always yields the same result, so replaying a
/// persisted stream-event log reproduces the UI state exactly.
/// </para>
/// <para>
/// Harness events with no dedicated stream kind (thought, tool call, tool-call update, permission request, plan)
/// are carried as <see cref="UnknownPayload"/> with <see cref="UnknownPayload.OriginalKind"/> set to the harness kind
/// name (<c>thought</c>, <c>tool_call</c>, <c>tool_call_update</c>, <c>permission_request</c>, <c>plan</c>). Their
/// <see cref="UnknownPayload.Raw"/> object carries a <c>kind</c> member equal to that name, so the kind survives
/// serialization. Nothing is dropped silently.
/// </para>
/// </remarks>
public static class HarnessEventProjection
{
    private const string ThoughtKind = "thought";
    private const string ToolCallKind = "tool_call";
    private const string ToolCallUpdateKind = "tool_call_update";
    private const string PermissionRequestKind = "permission_request";
    private const string PlanKind = "plan";

    /// <summary>Folds one harness event into the UI state.</summary>
    /// <param name="state">The state before the event.</param>
    /// <param name="evt">The harness event.</param>
    /// <returns>The state after the event.</returns>
    public static ProjectionState Fold(ProjectionState state, HarnessEvent evt)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(evt);

        return ApplyPayload(state, ToPayload(evt));
    }

    /// <summary>Folds one stream event into the UI state. Replaying a log is a fold of this overload from <see cref="ProjectionState.Empty"/>.</summary>
    /// <param name="state">The state before the event.</param>
    /// <param name="evt">The stream event.</param>
    /// <returns>The state after the event.</returns>
    public static ProjectionState Fold(ProjectionState state, StreamEvent evt)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(evt);

        return ApplyPayload(state, evt.Payload);
    }

    /// <summary>Maps one harness event to the stream events that record it.</summary>
    /// <param name="startSeq">The sequence number of the first produced event.</param>
    /// <param name="sessionId">The owning session identifier.</param>
    /// <param name="correlationId">The end-to-end correlation identifier.</param>
    /// <param name="timestamp">The timestamp to stamp on each produced event.</param>
    /// <param name="evt">The harness event.</param>
    /// <returns>The produced stream events in Seq order. Every harness event yields exactly one stream event.</returns>
    public static IReadOnlyList<StreamEvent> ToStreamEvents(long startSeq, string sessionId, string correlationId, DateTimeOffset timestamp, HarnessEvent evt)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(correlationId);
        ArgumentNullException.ThrowIfNull(evt);

        return
        [
            new StreamEvent
            {
                Seq = startSeq,
                SessionId = sessionId,
                CorrelationId = correlationId,
                Timestamp = timestamp,
                Payload = ToPayload(evt),
            },
        ];
    }

    private static StreamEventPayload ToPayload(HarnessEvent evt) => evt switch
    {
        TextDeltaEvent text => new TextDeltaPayload { Delta = text.Text },
        UiBlockEvent block => new UiBlockPayload { Block = block.Block },
        ErrorEvent error => new ErrorPayload { Message = error.Message, Retryable = error.Retryable },
        CompletedEvent completed => new CompletedPayload { Reason = completed.Reason },
        ThoughtEvent thought => Unknown(ThoughtKind, ("text", thought.Text)),
        ToolCallEvent call => Unknown(ToolCallKind, ("callId", call.CallId), ("name", call.Name), ("payloadJson", call.PayloadJson)),
        ToolCallUpdateEvent update => Unknown(ToolCallUpdateKind, ("callId", update.CallId), ("status", update.Status), ("payloadJson", update.PayloadJson)),
        PermissionRequestEvent permission => Unknown(PermissionRequestKind, ("callId", permission.CallId), ("name", permission.Name), ("payloadJson", permission.PayloadJson)),
        PlanEvent plan => Unknown(PlanKind, ("markdown", plan.Markdown)),
        _ => throw new ArgumentOutOfRangeException(nameof(evt), evt.GetType(), "No stream mapping exists for this harness event."),
    };

    private static UnknownPayload Unknown(string kind, params (string Name, string Value)[] fields) =>
        new() { OriginalKind = kind, Raw = BuildRaw(kind, fields) };

    /// <summary>
    /// Builds the verbatim payload object for an unknown-kind stream event: a JSON object with a <c>kind</c>
    /// member followed by the given string fields.
    /// </summary>
    /// <param name="kind">The kind name, written as the <c>kind</c> member.</param>
    /// <param name="fields">The string fields to include.</param>
    /// <returns>A cloned JSON object element.</returns>
    internal static JsonElement BuildRaw(string kind, params (string Name, string Value)[] fields)
    {
        var obj = new JsonObject { ["kind"] = kind };
        foreach (var (name, value) in fields)
        {
            obj[name] = value;
        }

        using var doc = JsonDocument.Parse(obj.ToJsonString());
        return doc.RootElement.Clone();
    }

    private static ProjectionState ApplyPayload(ProjectionState state, StreamEventPayload payload) => payload switch
    {
        TextDeltaPayload text => state with { StreamingText = state.StreamingText + text.Delta },
        UiBlockPayload block => Append(Flush(state), block.Block),
        ErrorPayload error => AddBlock(state, "error", id => new ErrorBlock { Id = id, Message = error.Message, Retryable = error.Retryable }),
        CompletedPayload completed => Flush(state) with { CompletionReason = completed.Reason },
        UnknownPayload unknown => ApplyUnknown(state, unknown),
        _ => throw new ArgumentOutOfRangeException(nameof(payload), payload.GetType(), "No projection exists for this payload."),
    };

    private static ProjectionState ApplyUnknown(ProjectionState state, UnknownPayload unknown)
    {
        var raw = unknown.Raw;
        switch (unknown.OriginalKind)
        {
            case ThoughtKind:
                // Thoughts are kept in the log but deliberately not rendered as UI blocks.
                return state;

            case ToolCallKind when TryString(raw, "callId", out var callId) && TryString(raw, "name", out var name):
                return Append(Flush(state), new ToolCallBlock
                {
                    Id = callId,
                    CallId = callId,
                    ToolName = name,
                    Status = "running",
                    Arguments = ParseArguments(raw),
                });

            case ToolCallUpdateKind when TryString(raw, "callId", out var updateId) && TryString(raw, "status", out var status):
                {
                    var index = FindToolCall(state.Blocks, updateId);
                    if (index < 0)
                    {
                        return AddBlock(state, "unknown", id => Unrecognized(id, unknown));
                    }

                    var existing = (ToolCallBlock)state.Blocks[index];
                    var updated = new List<UiBlock>(state.Blocks);
                    updated[index] = existing with { Status = status };
                    return state with { Blocks = updated };
                }

            case PermissionRequestKind when TryString(raw, "callId", out var permissionId) && TryString(raw, "name", out var permissionName):
                return AddBlock(state, "permission", _ => new PermissionBlock
                {
                    Id = permissionId,
                    Prompt = permissionName,
                    Options = ReadOptions(raw),
                });

            case PlanKind when TryString(raw, "markdown", out var markdown):
                return AddBlock(state, "plan", id => new PlanBlock
                {
                    Id = id,
                    Steps = markdown.Split('\n').Select(static line => line.Trim()).Where(static line => line.Length > 0).ToArray(),
                });

            default:
                return AddBlock(state, "unknown", id => Unrecognized(id, unknown));
        }
    }

    private static UnknownBlock Unrecognized(string id, UnknownPayload unknown) => new()
    {
        Id = id,
        OriginalKind = unknown.OriginalKind,
        Raw = unknown.Raw,
    };

    private static ProjectionState Flush(ProjectionState state)
    {
        if (state.StreamingText.Length == 0)
        {
            return state;
        }

        return state with
        {
            Blocks = [.. state.Blocks, new MarkdownBlock { Id = NextId(state, "markdown"), Markdown = state.StreamingText }],
            StreamingText = string.Empty,
        };
    }

    private static ProjectionState Append(ProjectionState state, UiBlock block) =>
        state with { Blocks = [.. state.Blocks, block] };

    /// <summary>Flushes streamed text, then appends a block whose identifier is derived from the flushed state.</summary>
    private static ProjectionState AddBlock(ProjectionState state, string prefix, Func<string, UiBlock> make)
    {
        var flushed = Flush(state);
        return Append(flushed, make(NextId(flushed, prefix)));
    }

    private static string NextId(ProjectionState state, string prefix) => $"{prefix}-{state.Blocks.Count}";

    private static int FindToolCall(IReadOnlyList<UiBlock> blocks, string callId)
    {
        for (var i = blocks.Count - 1; i >= 0; i--)
        {
            if (blocks[i] is ToolCallBlock call && string.Equals(call.CallId, callId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryString(JsonElement raw, string name, out string value)
    {
        if (raw.ValueKind == JsonValueKind.Object
            && raw.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static JsonElement ParseArguments(JsonElement raw)
    {
        // The arguments are stored as the harness's payload JSON text; a non-JSON payload is kept as a JSON string.
        var text = TryString(raw, "payloadJson", out var payloadJson) ? payloadJson : "{}";
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var doc = JsonDocument.Parse(JsonValue.Create(text)!.ToJsonString());
            return doc.RootElement.Clone();
        }
    }

    private static string[] ReadOptions(JsonElement raw)
    {
        if (!TryString(raw, "payloadJson", out var payloadJson))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("options", out var options)
                && options.ValueKind == JsonValueKind.Array)
            {
                return options.EnumerateArray()
                    .Where(static option => option.ValueKind == JsonValueKind.String)
                    .Select(static option => option.GetString() ?? string.Empty)
                    .ToArray();
            }
        }
        catch (JsonException)
        {
            // A payload that is not JSON carries no verbatim options.
        }

        return [];
    }
}

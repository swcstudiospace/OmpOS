using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;

namespace OmpOS.GenUi;

/// <summary>
/// The single pure fold from stream events to <see cref="UiTree"/> (UI-04).
/// </summary>
/// <remarks>
/// <para>
/// Semantics are equal to <c>HarnessEventProjection</c> for every payload kind it handles:
/// text deltas accumulate and flush into markdown blocks at block boundaries, tool-call updates
/// patch the matching card in place, thoughts are kept in the log but never rendered, and every
/// other unknown shape is preserved as an <see cref="UnknownBlock"/>.
/// In addition, <c>prompt_accepted</c> and <c>user_message</c> fold to exactly one <see cref="UserMessageBlock"/>
/// carrying the prompt text, so the user's own turn renders ahead of the assistant output.
/// Folding the same events from the same tree always yields the same result.
/// </para>
/// <para>
/// Unlike the harness projection, this fold never throws: a payload CLR type with no projection
/// degrades to an <see cref="UnknownBlock"/> carrying the payload's JSON, and malformed raw shapes
/// (array, string, number, or null roots) fall through the same unknown path instead of blanking
/// the view.
/// </para>
/// </remarks>
public static class UiFolder
{
    private const string ThoughtKind = "thought";
    private const string ToolCallKind = "tool_call";
    private const string ToolCallUpdateKind = "tool_call_update";
    private const string PermissionRequestKind = "permission_request";
    private const string PlanKind = "plan";
    private const string PromptAcceptedKind = "prompt_accepted";
    private const string FailedTurnReason = "error";

    /// <summary>Folds one stream event into the UI tree. Replaying a log is a fold of this overload from <see cref="UiTree.Empty"/>.</summary>
    /// <param name="state">The tree before the event.</param>
    /// <param name="evt">The stream event.</param>
    /// <returns>The tree after the event.</returns>
    public static UiTree Fold(UiTree state, StreamEvent evt)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(evt);

        return ApplyPayload(state, evt.Payload);
    }

    private static UiTree ApplyPayload(UiTree state, StreamEventPayload payload) => payload switch
    {
        TextDeltaPayload text => state with { StreamingText = state.StreamingText + text.Delta },
        UiBlockPayload block => Append(Flush(state), block.Block),
        UserMessagePayload user => AddBlock(state, "user", id => new UserMessageBlock { Id = id, Text = user.Text }),
        ErrorPayload error => AddBlock(state, "error", id => new ErrorBlock { Id = id, Message = error.Message, Retryable = error.Retryable }) with { CompletionReason = FailedTurnReason },
        CompletedPayload completed => Flush(state) with { CompletionReason = completed.Reason },
        DiagnosticPayload => state,
        UnknownPayload unknown => ApplyUnknown(state, unknown),
        _ => AddBlock(state, "unknown", id => new UnknownBlock { Id = id, OriginalKind = payload.GetType().Name, Raw = ToRaw(payload) }),
    };

    private static JsonElement ToRaw(StreamEventPayload payload)
    {
        var obj = new JsonObject { ["kind"] = payload.GetType().Name };
        using var doc = JsonDocument.Parse(obj.ToJsonString());
        return doc.RootElement.Clone();
    }

    private static UiTree ApplyUnknown(UiTree state, UnknownPayload unknown)
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

            case PromptAcceptedKind:
                // The user's own turn: exactly one user-role message carrying the prompt text,
                // appended in event order ahead of this turn's assistant output. Never an unknown block.
                return AddBlock(state, "user", id => new UserMessageBlock { Id = id, Text = ReadPromptText(raw) });

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

    private static UiTree Flush(UiTree state)
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

    private static UiTree Append(UiTree state, UiBlock block) =>
        state with { Blocks = [.. state.Blocks, block] };

    /// <summary>Flushes streamed text, then appends a block whose identifier is derived from the flushed state.</summary>
    private static UiTree AddBlock(UiTree state, string prefix, Func<string, UiBlock> make)
    {
        var flushed = Flush(state);
        return Append(flushed, make(NextId(flushed, prefix)));
    }

    private static string NextId(UiTree state, string prefix) => $"{prefix}-{state.Blocks.Count}";

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

    /// <summary>Reads the prompt text from a <c>prompt_accepted</c> payload; missing or non-string prompts yield empty text, never an unknown block.</summary>
    private static string ReadPromptText(JsonElement raw) =>
        TryString(raw, "prompt", out var prompt) ? prompt : string.Empty;

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

    /// <summary>
    /// Reads the verbatim permission options from the request payload. The card is informational:
    /// the decision is made by policy, so the options render read-only. When the payload carries
    /// no usable options, falls back to a single non-destructive <c>Dismiss</c> entry.
    /// </summary>
    private static string[] ReadOptions(JsonElement raw)
    {
        if (TryString(raw, "payloadJson", out var payloadJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(payloadJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("options", out var options)
                    && options.ValueKind == JsonValueKind.Array)
                {
                    var verbatim = options.EnumerateArray()
                        .Where(static option => option.ValueKind == JsonValueKind.String)
                        .Select(static option => option.GetString() ?? string.Empty)
                        .Where(static option => option.Length > 0)
                        .ToArray();
                    if (verbatim.Length > 0)
                    {
                        return verbatim;
                    }
                }
            }
            catch (JsonException)
            {
                // A payload that is not JSON carries no verbatim options.
            }
        }

        return ["Dismiss"];
    }
}

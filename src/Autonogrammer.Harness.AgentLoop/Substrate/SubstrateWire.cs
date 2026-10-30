using System.Text.Json;

namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>
/// The JSON shapes the Substrate server speaks, as tolerant DTOs for the source-generated <see cref="SubstrateJsonContext"/>.
/// Every member of a vendor reply is nullable and unknown members are skipped, so a server upgrade never breaks a read. The
/// DTOs for a ledger event carry only the fields a panel may show; the summary and the other payload keys have no member and
/// are therefore dropped by the parser.
/// </summary>
internal static class SubstrateWire
{
    /// <summary>Parses a JSON value, returning null when the text is empty or malformed.</summary>
    /// <typeparam name="T">The DTO type.</typeparam>
    /// <param name="text">The JSON text.</param>
    /// <param name="typeInfo">The source-generated metadata of <typeparamref name="T"/>.</param>
    /// <returns>The value, or null.</returns>
    internal static T? TryParse<T>(string text, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(text, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Parses a JSON value held as bytes, returning null when it is empty or malformed.</summary>
    /// <typeparam name="T">The DTO type.</typeparam>
    /// <param name="utf8">The JSON bytes.</param>
    /// <param name="typeInfo">The source-generated metadata of <typeparamref name="T"/>.</param>
    /// <returns>The value, or null.</returns>
    internal static T? TryParse<T>(ReadOnlySpan<byte> utf8, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        if (utf8.IsEmpty)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(utf8, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads a string member that a vendor may have sent as another JSON type.</summary>
    /// <param name="value">The member value.</param>
    /// <returns>The string, or null when the member is absent or not a string.</returns>
    internal static string? AsString(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.String } element ? element.GetString() : null;

    /// <summary>The <c>GET /healthz</c> reply.</summary>
    internal sealed class HealthDto
    {
        /// <summary>Gets the index store flag.</summary>
        public bool? Index { get; init; }

        /// <summary>Gets the ledger store flag.</summary>
        public bool? Greptime { get; init; }

        /// <summary>Gets the events-writable flag.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("eventsWritable")]
        public bool? EventsWritable { get; init; }
    }

    /// <summary>The <c>POST /brief</c> request body.</summary>
    internal sealed class BriefRequestDto
    {
        /// <summary>Gets the repository.</summary>
        public string? Repo { get; init; }

        /// <summary>Gets the branch.</summary>
        public string? Branch { get; init; }

        /// <summary>Gets the Graph identifier.</summary>
        public string? GraphId { get; init; }

        /// <summary>Gets the surface.</summary>
        public string? Surface { get; init; }

        /// <summary>Gets the profile.</summary>
        public string? Profile { get; init; }
    }

    /// <summary>The <c>payload</c> member of an emitted event.</summary>
    internal sealed class EmitPayloadDto
    {
        /// <summary>Gets the action.</summary>
        public string? Action { get; init; }

        /// <summary>Gets the item.</summary>
        public string? Item { get; init; }

        /// <summary>Gets the outcome.</summary>
        public string? Outcome { get; init; }

        /// <summary>Gets the steering mode.</summary>
        public string? Mode { get; init; }

        /// <summary>Gets the length.</summary>
        public int? Length { get; init; }
    }

    /// <summary>The <c>POST /events</c> request body.</summary>
    internal sealed class EmitRequestDto
    {
        /// <summary>Gets the event kind.</summary>
        public string? Kind { get; init; }

        /// <summary>Gets the summary.</summary>
        public string? Summary { get; init; }

        /// <summary>Gets the session identifier.</summary>
        public string? SessionId { get; init; }

        /// <summary>Gets the Graph identifier.</summary>
        public string? GraphId { get; init; }

        /// <summary>Gets the surface.</summary>
        public string? Surface { get; init; }

        /// <summary>Gets the actor.</summary>
        public string? Actor { get; init; }

        /// <summary>Gets the payload.</summary>
        public EmitPayloadDto? Payload { get; init; }
    }

    /// <summary>The <c>POST /events</c> reply.</summary>
    internal sealed class EmitReplyDto
    {
        /// <summary>Gets the event identifier.</summary>
        public JsonElement? Id { get; init; }

        /// <summary>Gets the stored flag.</summary>
        public bool? Stored { get; init; }
    }

    /// <summary>The <c>POST /events/query</c> request body.</summary>
    internal sealed class QueryRequestDto
    {
        /// <summary>Gets the session filter.</summary>
        public string? SessionId { get; init; }

        /// <summary>Gets the graph filter.</summary>
        public string? GraphId { get; init; }

        /// <summary>Gets the row limit.</summary>
        public int? Limit { get; init; }

        /// <summary>Gets the order.</summary>
        public string? Order { get; init; }

        /// <summary>Gets a value requesting payloads.</summary>
        public bool? IncludePayload { get; init; }
    }

    /// <summary>The declared payload keys of a ledger event. Every other key is dropped.</summary>
    internal sealed class LedgerPayloadDto
    {
        /// <summary>Gets <c>payload.action</c>.</summary>
        public JsonElement? Action { get; init; }

        /// <summary>Gets <c>payload.item</c>.</summary>
        public JsonElement? Item { get; init; }

        /// <summary>Gets <c>payload.outcome</c>.</summary>
        public JsonElement? Outcome { get; init; }
    }

    /// <summary>One ledger event. The summary has no member here on purpose.</summary>
    internal sealed class LedgerEventDto
    {
        /// <summary>Gets the event identifier.</summary>
        public JsonElement? Id { get; init; }

        /// <summary>Gets the source time.</summary>
        public JsonElement? Ts { get; init; }

        /// <summary>Gets the kind.</summary>
        public JsonElement? Kind { get; init; }

        /// <summary>Gets the actor.</summary>
        public JsonElement? Actor { get; init; }

        /// <summary>Gets the declared payload keys.</summary>
        public LedgerPayloadDto? Payload { get; init; }
    }

    /// <summary>The <c>POST /events/query</c> reply.</summary>
    internal sealed class QueryReplyDto
    {
        /// <summary>Gets the events.</summary>
        public List<LedgerEventDto?>? Events { get; init; }

        /// <summary>Gets the truncated flag.</summary>
        public bool? Truncated { get; init; }
    }

    /// <summary>One content block of a JSON-RPC tool result.</summary>
    internal sealed class RpcContentDto
    {
        /// <summary>Gets the block type.</summary>
        public string? Type { get; init; }

        /// <summary>Gets the block text.</summary>
        public string? Text { get; init; }
    }

    /// <summary>The <c>result</c> of a JSON-RPC <c>tools/call</c> reply.</summary>
    internal sealed class RpcResultDto
    {
        /// <summary>Gets the content blocks.</summary>
        public List<RpcContentDto?>? Content { get; init; }

        /// <summary>Gets the tool error flag.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("isError")]
        public bool? IsError { get; init; }
    }

    /// <summary>The <c>error</c> of a JSON-RPC reply. The message is deliberately not read.</summary>
    internal sealed class RpcErrorDto
    {
        /// <summary>Gets the error code.</summary>
        public int? Code { get; init; }
    }

    /// <summary>A JSON-RPC reply envelope.</summary>
    internal sealed class RpcEnvelopeDto
    {
        /// <summary>Gets the result.</summary>
        public RpcResultDto? Result { get; init; }

        /// <summary>Gets the error.</summary>
        public RpcErrorDto? Error { get; init; }
    }

    /// <summary>One memory entry as <c>memory_search</c> returns it.</summary>
    internal sealed class MemoryEntryDto
    {
        /// <summary>Gets the entry identifier.</summary>
        public string? Id { get; init; }

        /// <summary>Gets the scope.</summary>
        public string? Scope { get; init; }

        /// <summary>Gets the kind.</summary>
        public string? Kind { get; init; }

        /// <summary>Gets the subject.</summary>
        public string? Subject { get; init; }

        /// <summary>Gets the body text.</summary>
        public string? Text { get; init; }

        /// <summary>Gets the creation time.</summary>
        public string? Created { get; init; }

        /// <summary>Gets the identifier of the entry that replaced this one.</summary>
        public string? SupersededBy { get; init; }

        /// <summary>Gets the expired flag.</summary>
        public bool? Expired { get; init; }
    }

    /// <summary>The paged <c>memory_search</c> reply.</summary>
    internal sealed class MemoryPageDto
    {
        /// <summary>Gets the entries.</summary>
        public List<MemoryEntryDto>? Entries { get; init; }
    }

    /// <summary>One node of a graph as <c>graph_get</c> returns it. The lease identifier has no member here on purpose.</summary>
    internal sealed class GraphNodeDto
    {
        /// <summary>Gets the node identifier.</summary>
        public string? NodeId { get; init; }

        /// <summary>Gets the node state.</summary>
        public string? State { get; init; }

        /// <summary>Gets the surface holding the lease.</summary>
        public string? ClaimedBy { get; init; }

        /// <summary>Gets the holder's session identifier.</summary>
        public string? ClaimSession { get; init; }

        /// <summary>Gets the lease expiry.</summary>
        public string? LeaseExpiresAt { get; init; }
    }

    /// <summary>A graph as <c>graph_get</c> returns it.</summary>
    internal sealed class GraphViewDto
    {
        /// <summary>Gets the graph identifier.</summary>
        public string? GraphId { get; init; }

        /// <summary>Gets the nodes.</summary>
        public List<GraphNodeDto?>? Nodes { get; init; }
    }

    /// <summary>The <c>issue_get</c> reply.</summary>
    internal sealed class IssueDto
    {
        /// <summary>Gets the found flag.</summary>
        public bool? Found { get; init; }

        /// <summary>Gets the issue title.</summary>
        public string? Title { get; init; }
    }
}

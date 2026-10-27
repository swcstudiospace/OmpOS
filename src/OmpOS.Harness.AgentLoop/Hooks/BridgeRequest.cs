using System.Text.Json;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// One hook request as the omp hooks extension posts it to the bridge. The member names on the wire are camelCase. Members the
/// extension adds later are skipped, so a newer extension does not break an older host.
/// </summary>
public sealed record BridgeRequest
{
    /// <summary>The <see cref="Kind"/> of a request that names a Cursor hook event. It is also the value a missing kind gets.</summary>
    public const string CursorKind = "cursor";

    /// <summary>The <see cref="Kind"/> of a request that names an omp extension event.</summary>
    public const string OmpKind = "omp";

    /// <summary>
    /// Gets the event name: a Cursor hook event (camelCase, for example <c>preToolUse</c>) when <see cref="Kind"/> is
    /// <see cref="CursorKind"/>, or an omp extension event (for example <c>tool_call</c>) when it is <see cref="OmpKind"/>.
    /// </summary>
    public required string Event { get; init; }

    /// <summary>Gets the kind of the event, <see cref="CursorKind"/> or <see cref="OmpKind"/>. The server turns a missing kind into <see cref="CursorKind"/>.</summary>
    public string Kind { get; init; } = CursorKind;

    /// <summary>Gets the omp tool call identifier when the event belongs to a tool call.</summary>
    public string? ToolCallId { get; init; }

    /// <summary>Gets the omp session identifier when the extension knows it.</summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the event-specific payload. For a Cursor event it holds only the event-specific fields, named as Cursor names them;
    /// for an omp event it is a JSON-safe copy of the omp event object. A request the server produces always has a JSON object
    /// here, empty when the client sent none. A request built by hand may leave it at its default, whose kind is undefined.
    /// </summary>
    public JsonElement Payload { get; init; }
}

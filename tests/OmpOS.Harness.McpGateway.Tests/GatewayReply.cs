using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace OmpOS.Harness.McpGateway.Tests;

/// <summary>One HTTP response from the gateway, with the JSON-RPC body parsed on demand.</summary>
/// <param name="Status">The HTTP status code.</param>
/// <param name="Body">The response body; empty when there is none.</param>
/// <param name="Headers">The response headers.</param>
/// <param name="ContentHeaders">The headers HTTP classifies as content headers, such as <c>Allow</c>.</param>
internal sealed record GatewayReply(int Status, string Body, HttpResponseHeaders Headers, HttpContentHeaders ContentHeaders)
{
    /// <summary>The parsed JSON body, or <c>null</c> when the body is empty.</summary>
    public JsonNode? Json => string.IsNullOrEmpty(Body) ? null : JsonNode.Parse(Body);

    /// <summary>The <c>Mcp-Session-Id</c> response header, or <c>null</c>.</summary>
    public string? SessionId => HeaderValue("Mcp-Session-Id");

    /// <summary>The JSON-RPC error code, or <c>null</c> when the body is not an error.</summary>
    public int? ErrorCode => Json?["error"]?["code"]?.GetValue<int>();

    /// <summary>The tool names listed in a <c>tools/list</c> result, in order.</summary>
    public IReadOnlyList<string> ToolNames =>
        Json?["result"]?["tools"]?.AsArray().Select(static tool => tool!["name"]!.GetValue<string>()).ToList() ?? [];

    /// <summary>Returns the values of a response header (or content header) joined with a comma and a space, or <c>null</c> when it is absent.</summary>
    /// <param name="name">The header name.</param>
    /// <returns>The header value.</returns>
    public string? HeaderValue(string name) =>
        Headers.TryGetValues(name, out var values) || ContentHeaders.TryGetValues(name, out values) ? string.Join(", ", values) : null;
}

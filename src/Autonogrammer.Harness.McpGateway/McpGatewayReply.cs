namespace Autonogrammer.Harness.McpGateway;

/// <summary>What the dispatcher wants sent back for one HTTP <c>POST</c>.</summary>
/// <param name="StatusCode">The HTTP status code.</param>
/// <param name="Body">The JSON body, or empty when the response has none.</param>
/// <param name="TransportSessionId">The <c>Mcp-Session-Id</c> to return, set only by <c>initialize</c>.</param>
internal sealed record McpGatewayReply(int StatusCode, ReadOnlyMemory<byte> Body, string? TransportSessionId);

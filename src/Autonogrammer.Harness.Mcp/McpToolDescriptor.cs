using System.Text.Json;

namespace Autonogrammer.Harness.Mcp;

/// <summary>A tool as registered from a connected server: its identity, description and input schema.</summary>
/// <param name="Id">The server and tool identity.</param>
/// <param name="Description">The description the server advertised, if any.</param>
/// <param name="InputSchema">The JSON Schema for the tool's arguments, exactly as the server advertised it.</param>
public sealed record McpToolDescriptor(McpToolId Id, string? Description, JsonElement InputSchema);

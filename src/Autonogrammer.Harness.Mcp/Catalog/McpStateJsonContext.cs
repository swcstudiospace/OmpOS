using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Mcp.Catalog;

/// <summary>Source-generated JSON metadata for the persisted MCP state. Unmapped members are rejected, so a file written by another version or edited by hand is reported as damaged instead of half-read.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(McpStateDocument))]
internal sealed partial class McpStateJsonContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;

namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// Source-generated JSON metadata for the persisted recent sets. Property names are camelCase. Unmapped members are rejected, so a
/// file written by another version or edited by hand is reported as corrupt instead of half-read.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(WorkspaceSetDocument))]
internal sealed partial class WorkspaceSetJsonContext : JsonSerializerContext
{
}

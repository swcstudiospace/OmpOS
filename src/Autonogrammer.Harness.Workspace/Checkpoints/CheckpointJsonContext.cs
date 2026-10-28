using System.Text.Json.Serialization;

namespace Autonogrammer.Workspace.Checkpoints;

/// <summary>Source-generated JSON metadata for the persisted checkpoint timeline. Unmapped members are rejected, so a file written by another version or edited by hand is read as an empty timeline instead of half-read.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CheckpointDocument))]
internal sealed partial class CheckpointJsonContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;

namespace OmpOS.Harness.AgentLoop.Gsd;

/// <summary>
/// Source-generated JSON metadata for the workflow tool's output. Property names are snake_case, matching the tool. Unmapped
/// members are skipped, so a new field in a later tool release never breaks the reader.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(GsdRoadmapDocument))]
internal sealed partial class GsdJsonContext : JsonSerializerContext
{
}

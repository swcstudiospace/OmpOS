using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>
/// Source-generated JSON metadata for the vendor files the stage observers read. Reflection-based serialization is not used.
/// The files belong to other projects, so reading is tolerant: unmapped members are skipped, names are matched without regard
/// to case, and trailing commas and comments do not fail a parse. Property names are fixed on the DTOs, not by a naming policy.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    AllowTrailingCommas = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip)]
[JsonSerializable(typeof(UltrathinkSessionDocument))]
[JsonSerializable(typeof(SwarmLiveDocument))]
[JsonSerializable(typeof(SwarmEventRecord))]
internal sealed partial class StagesJsonContext : JsonSerializerContext
{
}

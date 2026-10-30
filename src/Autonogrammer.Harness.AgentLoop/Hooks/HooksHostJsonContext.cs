using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>Source-generated JSON metadata for <see cref="HooksHostDocument"/>. Names are camelCase; unknown members are skipped.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    WriteIndented = true)]
[JsonSerializable(typeof(HooksHostDocument))]
internal sealed partial class HooksHostJsonContext : JsonSerializerContext
{
}

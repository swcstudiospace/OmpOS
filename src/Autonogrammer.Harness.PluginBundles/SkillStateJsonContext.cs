using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Source-generated JSON metadata for <c>skills-state.json</c>.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(SkillStateDocument))]
internal sealed partial class SkillStateJsonContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Plugins;

/// <summary>
/// Source-generated JSON metadata for manifests, hook identities and the persisted plugin state. Reflection-based
/// serialization is not used. Property names are camelCase, matching the manifest schema. Unmapped members are rejected,
/// so a misspelled key cannot be silently ignored.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ManifestDocument))]
[JsonSerializable(typeof(HookIdentityDocument))]
[JsonSerializable(typeof(PluginStateDocument))]
internal sealed partial class PluginJsonContext : JsonSerializerContext
{
}

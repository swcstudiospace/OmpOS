using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Source-generated JSON metadata for a stored plugin manifest. Unmapped members are rejected.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(PluginBundleManifest))]
public sealed partial class PluginBundleJsonContext : JsonSerializerContext
{
}

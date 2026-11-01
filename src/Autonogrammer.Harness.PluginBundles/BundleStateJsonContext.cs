using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Source-generated JSON metadata for <c>plugins-state.json</c>.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(BundleStateDocument))]
internal sealed partial class BundleStateJsonContext : JsonSerializerContext
{
}

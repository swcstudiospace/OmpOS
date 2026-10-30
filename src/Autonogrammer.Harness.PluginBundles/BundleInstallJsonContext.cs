using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.PluginBundles;

/// <summary>Source-generated JSON metadata for the install sidecar and <c>.mcp.json</c>.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(BundleInstallSidecar))]
[JsonSerializable(typeof(BundleMcpFileDocument))]
internal sealed partial class BundleInstallJsonContext : JsonSerializerContext
{
}

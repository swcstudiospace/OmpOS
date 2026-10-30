using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Registry;

/// <summary>
/// Source-generated JSON metadata for the export file and for the canonical entry form that the checksum covers. Property
/// names are camelCase. Unmapped members are rejected, so a misspelled key cannot be silently ignored.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(RegistryExportDocument))]
[JsonSerializable(typeof(RegistryEntry[]))]
internal sealed partial class RegistryJsonContext : JsonSerializerContext
{
}

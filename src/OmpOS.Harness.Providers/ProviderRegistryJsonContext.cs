using System.Text.Json.Serialization;

namespace OmpOS.Harness.Providers;

/// <summary>
/// Source-generated JSON metadata for the provider registry file. Reflection-based serialization is not used. Property names
/// are camelCase. Unmapped members are rejected, so a misspelled key cannot be silently dropped.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
[JsonSerializable(typeof(ProviderRegistryDocument))]
internal sealed partial class ProviderRegistryJsonContext : JsonSerializerContext
{
}

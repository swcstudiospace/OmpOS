using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Providers;

/// <summary>
/// Source-generated JSON metadata for the embedded catalogue data. Reflection-based serialization is not used. Property names are
/// camelCase and enum values are their camelCase names. Unmapped members are rejected and constructor members without a default are
/// required, so a misspelled or missing key in a data file fails the load instead of being dropped.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(CatalogueDocument))]
internal sealed partial class CatalogueJsonContext : JsonSerializerContext
{
}

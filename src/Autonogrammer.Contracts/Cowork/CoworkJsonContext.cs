using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Cowork;

/// <summary>
/// Source-generated JSON metadata for Cowork wire types. Names are camelCase, null members are omitted, and statuses are strings.
/// An unknown status string is refused; it is never read as a default value.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CoworkSessionSummary))]
[JsonSerializable(typeof(CoworkSessionStatus))]
public sealed partial class CoworkJsonContext : JsonSerializerContext
{
}

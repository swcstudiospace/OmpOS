using System.Text.Json.Serialization;
using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// Source-generated JSON metadata for the persisted environment profiles. Property names are camelCase, enums are strings and unmapped members are
/// rejected, so a file written by another version or edited by hand is reported as damaged instead of half-read.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(EnvironmentStoreDocument))]
[JsonSerializable(typeof(EnvironmentProfile))]
internal sealed partial class EnvironmentStoreJsonContext : JsonSerializerContext
{
}

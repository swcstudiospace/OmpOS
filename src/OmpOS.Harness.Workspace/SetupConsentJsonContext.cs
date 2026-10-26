using System.Text.Json.Serialization;

namespace OmpOS.Workspace;

/// <summary>
/// Source-generated JSON metadata for the persisted setup consent. Property names are camelCase. Unmapped members are
/// rejected, so a file written by another version or edited by hand is reported as corrupt instead of half-read.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(SetupConsentDocument))]
internal sealed partial class SetupConsentJsonContext : JsonSerializerContext
{
}

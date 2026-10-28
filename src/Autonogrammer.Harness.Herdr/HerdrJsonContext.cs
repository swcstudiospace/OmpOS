using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Herdr;

/// <summary>
/// Source-generated JSON metadata for the herdr session store. Reflection-based serialization is not used. Property names
/// are camelCase. Unmapped members are rejected, so a misspelled key cannot be silently dropped.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true)]
[JsonSerializable(typeof(HerdrSessionDocument))]
[JsonSerializable(typeof(HerdrStoredSessionDocument))]
internal sealed partial class HerdrJsonContext : JsonSerializerContext
{
}

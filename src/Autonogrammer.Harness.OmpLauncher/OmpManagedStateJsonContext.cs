using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.OmpLauncher;

/// <summary>
/// Source-generated JSON metadata for <c>state.json</c>. Unknown members are skipped so a state file written by a newer
/// build still reads; absent optional members read as null.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(OmpManagedState))]
[JsonSerializable(typeof(OmpQuarantinedVersion))]
internal sealed partial class OmpManagedStateJsonContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;

namespace OmpOS.Harness.Terminal;

/// <summary>
/// Source-generated JSON metadata for <see cref="TerminalSettingsFile"/>. Unknown members are rejected so that a
/// misspelled key is reported instead of being ignored, and no reflection serialization is used.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(TerminalSettingsFile))]
internal sealed partial class TerminalSettingsJsonContext : JsonSerializerContext
{
}

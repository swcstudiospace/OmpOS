using System.Text.Json.Serialization;

namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>Source-generated JSON metadata for the display switches file. Unmapped members are rejected, so a file written by another version or edited by hand reads as the defaults instead of half-read.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(DisplaySettingsDocument))]
internal sealed partial class DisplaySettingsJsonContext : JsonSerializerContext
{
}

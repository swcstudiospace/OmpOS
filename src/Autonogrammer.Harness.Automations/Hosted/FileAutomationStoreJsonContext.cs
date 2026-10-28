using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Source-generated JSON metadata for the automation store file. Enums are written as strings. Unknown members are rejected so that a
/// misspelled or foreign key is reported as a corrupt file instead of being ignored.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(AutomationStoreDocument))]
internal sealed partial class FileAutomationStoreJsonContext : JsonSerializerContext
{
}

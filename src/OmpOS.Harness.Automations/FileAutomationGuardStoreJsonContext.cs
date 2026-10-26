using System.Text.Json.Serialization;

namespace OmpOS.Harness.Automations;

/// <summary>
/// Source-generated JSON metadata for the guard store file. Unknown members are rejected so that a misspelled or
/// foreign key is reported as a corrupt file instead of being ignored.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(GuardStoreDocument))]
internal sealed partial class FileAutomationGuardStoreJsonContext : JsonSerializerContext
{
}

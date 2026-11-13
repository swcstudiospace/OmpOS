using System.Text.Json.Serialization;
using Autonogrammer.Harness.Automations.Hosted;

namespace Autonogrammer.Api.Automations;

/// <summary>
/// Source-generated JSON metadata for the automation records <see cref="PostgresAutomationStore"/> keeps in <c>record</c>. The rules match
/// the file store's (camelCase names, enums as strings, nulls omitted, unknown members rejected). Reflection serialisation is off.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(UserAutomation))]
[JsonSerializable(typeof(AutomationRunEntry))]
internal sealed partial class PostgresAutomationJsonContext : JsonSerializerContext
{
}

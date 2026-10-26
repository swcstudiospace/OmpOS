using System.Text.Json.Serialization;

namespace OmpOS.Harness.AgentLoop.Sessions;

/// <summary>
/// Source-generated JSON metadata for the session registry's state file. Reflection-based serialization is not used. Property
/// names are camelCase. Unmapped members are skipped, so a newer writer's extra members do not break an older reader.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(AgentSessionDocument))]
internal sealed partial class AgentSessionJsonContext : JsonSerializerContext
{
}

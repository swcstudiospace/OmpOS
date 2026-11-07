using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Cowork.Projects;

/// <summary>Source-generated JSON for project sync records. Camel case, nulls omitted. No folders member is serialised.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProjectSyncRecord))]
[JsonSerializable(typeof(GlobalInstructionsRecord))]
[JsonSerializable(typeof(IReadOnlyList<ProjectSyncRecord>))]
[JsonSerializable(typeof(List<ProjectSyncRecord>))]
public sealed partial class CoworkProjectJsonContext : JsonSerializerContext
{
}

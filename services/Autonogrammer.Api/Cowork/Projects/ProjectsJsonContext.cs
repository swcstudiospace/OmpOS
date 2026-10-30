using System.Text.Json.Serialization;
using Autonogrammer.Contracts.Cowork.Projects;

namespace Autonogrammer.Api.Cowork.Projects;

/// <summary>Source-generated JSON for project routes. Camel case, nulls omitted. No folders member is written.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ProjectSyncRecord))]
[JsonSerializable(typeof(GlobalInstructionsRecord))]
[JsonSerializable(typeof(List<ProjectSyncRecord>))]
internal sealed partial class ProjectsJsonContext : JsonSerializerContext
{
}

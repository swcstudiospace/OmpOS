using System.Text.Json.Serialization;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Source-generated JSON for artifact routes.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ArtifactListDocument))]
[JsonSerializable(typeof(ArtifactSummaryDocument))]
[JsonSerializable(typeof(ArtifactGrantDocument))]
[JsonSerializable(typeof(List<ArtifactGrantDocument>))]
[JsonSerializable(typeof(AuditPayload))]
[JsonSerializable(typeof(RenderTicketDocument))]
internal sealed partial class ArtifactsJsonContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;

namespace Autonogrammer.Web.Services.Artifacts;

/// <summary>
/// Source-generated JSON metadata for the artifact bodies the web head reads and the Demo serves. The names are the camel-cased members of
/// the API's artifact documents. Reflection-free, so the trimmed WASM publish keeps them.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ArtifactListRecord))]
[JsonSerializable(typeof(ArtifactSummaryRecord))]
[JsonSerializable(typeof(RenderTicketRecord))]
internal sealed partial class ArtifactWebJsonContext : JsonSerializerContext
{
}

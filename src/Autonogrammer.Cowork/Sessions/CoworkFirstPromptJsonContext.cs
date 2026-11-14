using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Sessions;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CoworkFirstPromptDocument))]
internal sealed partial class CoworkFirstPromptJsonContext : JsonSerializerContext
{
}

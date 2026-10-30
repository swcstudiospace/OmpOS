using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Sessions;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CoworkSessionMetaDocument))]
internal sealed partial class CoworkSessionMetaJsonContext : JsonSerializerContext
{
}

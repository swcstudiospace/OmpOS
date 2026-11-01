using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Sessions;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CoworkProjectDocument))]
internal sealed partial class CoworkProjectJsonContext : JsonSerializerContext
{
}

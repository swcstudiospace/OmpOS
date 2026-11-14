using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Projects;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(GlobalInstructionsDocument))]
internal sealed partial class GlobalInstructionsJsonContext : JsonSerializerContext
{
}

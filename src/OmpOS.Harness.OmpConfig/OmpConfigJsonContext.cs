using System.Text.Json.Serialization;

namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// Source-generated JSON metadata for what omp prints: the model listing and the <c>modelRoles</c> object. Property names are
/// camelCase and members omp adds later are ignored, so a newer omp does not break reading.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(OmpModelsDocument))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class OmpConfigJsonContext : JsonSerializerContext
{
}

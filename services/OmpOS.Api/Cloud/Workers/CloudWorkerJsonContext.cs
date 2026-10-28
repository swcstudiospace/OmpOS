using System.Text.Json.Serialization;

namespace OmpOS.Api.Cloud;

/// <summary>Source-generated JSON metadata for the structured values handed to a worker as environment variables: the repository list, the protected branches and the resolved environment.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CloudWorkerRepository[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(EnvironmentConfig))]
internal sealed partial class CloudWorkerJsonContext : JsonSerializerContext
{
}

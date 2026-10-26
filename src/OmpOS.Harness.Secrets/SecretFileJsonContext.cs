using System.Text.Json.Serialization;

namespace OmpOS.Harness.Secrets;

/// <summary>Source-generated JSON metadata for <see cref="SecretFileDocument"/>. Property names are camelCase.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SecretFileDocument))]
internal sealed partial class SecretFileJsonContext : JsonSerializerContext
{
}

using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// Source-generated JSON metadata for <see cref="CloudTranscriptEntry"/> (one compact JSON object per volume line).
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CloudTranscriptEntry))]
public sealed partial class CloudTranscriptJsonContext : JsonSerializerContext
{
}

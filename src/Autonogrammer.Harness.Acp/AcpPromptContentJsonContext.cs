using System.Text.Json.Serialization;
using Autonogrammer.Harness.Acp.AcpProtocol;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// Source-generated serializer context for <c>session/prompt</c> requests that carry embedded resources. Use
/// <c>AcpPromptContentJsonContext.Default.RequestAcpPromptContentParams</c> as the type info.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(AcpFrames.Request<AcpPromptContentParams>))]
[JsonSerializable(typeof(AcpPromptBlock))]
[JsonSerializable(typeof(AcpEmbeddedResource))]
public sealed partial class AcpPromptContentJsonContext : JsonSerializerContext
{
}

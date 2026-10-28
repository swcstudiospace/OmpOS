using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>
/// Source-generated JSON metadata for the Substrate wire DTOs. Reflection-based serialization is not used. Property names are
/// snake_case as the server speaks them, null members are not written, and unknown members of a reply are skipped.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(SubstrateWire.HealthDto))]
[JsonSerializable(typeof(SubstrateWire.BriefRequestDto))]
[JsonSerializable(typeof(SubstrateWire.EmitRequestDto))]
[JsonSerializable(typeof(SubstrateWire.EmitReplyDto))]
[JsonSerializable(typeof(SubstrateWire.QueryRequestDto))]
[JsonSerializable(typeof(SubstrateWire.QueryReplyDto))]
[JsonSerializable(typeof(SubstrateWire.RpcEnvelopeDto))]
[JsonSerializable(typeof(SubstrateWire.MemoryPageDto))]
[JsonSerializable(typeof(List<SubstrateWire.MemoryEntryDto>))]
[JsonSerializable(typeof(SubstrateWire.GraphViewDto))]
[JsonSerializable(typeof(SubstrateWire.IssueDto))]
internal sealed partial class SubstrateJsonContext : JsonSerializerContext
{
}

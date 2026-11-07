using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>
/// Source-generated JSON metadata for every Dispatch frame and REST DTO. Names are camel case. Null members are omitted.
/// Reflection serialisation is off.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(DispatchOp))]
[JsonSerializable(typeof(DispatchCapability))]
[JsonSerializable(typeof(DispatchCommandState))]
[JsonSerializable(typeof(DispatchFailureReason))]
[JsonSerializable(typeof(DispatchCapabilitySnapshot))]
[JsonSerializable(typeof(DispatchChallengeFrame))]
[JsonSerializable(typeof(DispatchWelcomeFrame))]
[JsonSerializable(typeof(DispatchCmdFrame))]
[JsonSerializable(typeof(DispatchRevokeFrame))]
[JsonSerializable(typeof(DispatchErrFrame))]
[JsonSerializable(typeof(DispatchHelloFrame))]
[JsonSerializable(typeof(DispatchAckFrame))]
[JsonSerializable(typeof(DispatchEvtFrame))]
[JsonSerializable(typeof(DispatchResFrame))]
[JsonSerializable(typeof(DispatchHbFrame))]
[JsonSerializable(typeof(PairDispatchDeviceRequest))]
[JsonSerializable(typeof(PairDispatchDeviceResponse))]
[JsonSerializable(typeof(DispatchDeviceDto))]
[JsonSerializable(typeof(DispatchDeviceListResponse))]
[JsonSerializable(typeof(RenameDispatchDeviceRequest))]
[JsonSerializable(typeof(CreateDispatchCommandRequest))]
[JsonSerializable(typeof(DispatchCommandAcceptedResponse))]
[JsonSerializable(typeof(DispatchCommandDto))]
[JsonSerializable(typeof(DispatchCommandListResponse))]
[JsonSerializable(typeof(DispatchFeedEventDto))]
public sealed partial class DispatchJsonContext : JsonSerializerContext
{
}

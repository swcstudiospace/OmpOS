using System.Text.Json.Serialization;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Contracts.Controls;

/// <summary>
/// Source-generated JSON metadata for the session control shapes: steering, tool-approval list, configuration options, slash commands and
/// change review. It is a feature context of its own and does not grow the core contract context.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SteerRequest))]
[JsonSerializable(typeof(SteerResponse))]
[JsonSerializable(typeof(SteerMode))]
[JsonSerializable(typeof(SteerDelivery))]
[JsonSerializable(typeof(PendingToolApproval))]
[JsonSerializable(typeof(PendingToolApprovalsResponse))]
[JsonSerializable(typeof(ConfigOption))]
[JsonSerializable(typeof(ConfigOptionValue))]
[JsonSerializable(typeof(SessionConfigOptionsResponse))]
[JsonSerializable(typeof(SetConfigOptionRequest))]
[JsonSerializable(typeof(ConfigOptionUpdateEvent))]
[JsonSerializable(typeof(SessionCommand))]
[JsonSerializable(typeof(SessionCommandsResponse))]
[JsonSerializable(typeof(SessionChange))]
[JsonSerializable(typeof(SessionChangesResponse))]
[JsonSerializable(typeof(ChangeDecisionResponse))]
[JsonSerializable(typeof(ChangeKind))]
[JsonSerializable(typeof(ChangeReviewStatus))]
[JsonSerializable(typeof(StageEvent))]
[JsonSerializable(typeof(StageSource))]
[JsonSerializable(typeof(StageStatus))]
public sealed partial class SessionControlsJsonContext : JsonSerializerContext
{
}

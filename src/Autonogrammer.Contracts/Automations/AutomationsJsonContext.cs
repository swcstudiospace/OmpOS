using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Automations;

/// <summary>
/// Source-generated JSON metadata for the automation shapes. It is a feature context of its own: it adds shapes without growing the
/// core contract context. Names are camel case, null members are omitted.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SaveAutomationRequest))]
[JsonSerializable(typeof(SetAutomationEnabledRequest))]
[JsonSerializable(typeof(AutomationInfo))]
[JsonSerializable(typeof(AutomationSaveResponse))]
[JsonSerializable(typeof(AutomationListResponse))]
[JsonSerializable(typeof(AutomationRunInfo))]
[JsonSerializable(typeof(AutomationRunListResponse))]
[JsonSerializable(typeof(AutomationWebhookResponse))]
[JsonSerializable(typeof(AutomationCronPreviewResponse))]
[JsonSerializable(typeof(AutomationTriggerInfo))]
[JsonSerializable(typeof(AutomationTargetInfo))]
[JsonSerializable(typeof(AutomationRepositoryInfo))]
public sealed partial class AutomationsJsonContext : JsonSerializerContext
{
}

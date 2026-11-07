using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Cowork.Scheduling;

/// <summary>
/// Source-generated JSON for scheduled-task drafts. Camel case, nulls omitted. These documents are device-local; nothing here is synced.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ScheduledTaskDraft))]
[JsonSerializable(typeof(ScheduledApprovalMode))]
[JsonSerializable(typeof(ScheduledPreset))]
[JsonSerializable(typeof(ScheduledMissedRunPolicy))]
public sealed partial class SchedulingJsonContext : JsonSerializerContext
{
}

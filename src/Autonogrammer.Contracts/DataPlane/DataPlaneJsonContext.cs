using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.DataPlane;

/// <summary>
/// Source-generated JSON metadata for the data-plane shapes. Names are camelCase, null members are omitted and enums are written as strings.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(DataPlaneStatus))]
[JsonSerializable(typeof(DataPlaneStatus[]))]
[JsonSerializable(typeof(DataPlaneProblem))]
[JsonSerializable(typeof(DataPlaneComponent))]
[JsonSerializable(typeof(DataPlaneState))]
[JsonSerializable(typeof(DataPlaneReason))]
public sealed partial class DataPlaneJsonContext : JsonSerializerContext
{
}

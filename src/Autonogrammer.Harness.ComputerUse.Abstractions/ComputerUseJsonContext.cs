using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Source-generated JSON metadata for the denial catalogue and options types.
/// Reflection serialisation is disabled by the repository build property
/// <c>JsonSerializerIsReflectionEnabledByDefault=false</c>. Callers pass this context; they do not use reflection.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(ComputerUseOptions))]
[JsonSerializable(typeof(ComputerUseUserSettings))]
[JsonSerializable(typeof(DenialCode))]
[JsonSerializable(typeof(ComputerAvailability))]
[JsonSerializable(typeof(PostActionFrame))]
[JsonSerializable(typeof(Dictionary<string, int>))]
public sealed partial class ComputerUseJsonContext : JsonSerializerContext
{
}

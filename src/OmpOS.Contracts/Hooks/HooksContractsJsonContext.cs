using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Hooks;

/// <summary>
/// Source-generated JSON metadata for the hook stream event. It is a feature context of its own and does not grow the core contract
/// context. Names are camelCase and null members are not written.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HookRunEvent))]
[JsonSerializable(typeof(HookRunEvent[]))]
public sealed partial class HooksContractsJsonContext : JsonSerializerContext
{
}

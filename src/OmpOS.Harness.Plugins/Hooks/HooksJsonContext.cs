using System.Text.Json.Serialization;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Source-generated JSON metadata for the hook wire decision, the run log and the consent identity. Reflection-based
/// serialization is not used. Nulls are omitted.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HookDecisionWire))]
[JsonSerializable(typeof(HookRunLogLine))]
[JsonSerializable(typeof(HookIdentityV2Document))]
internal sealed partial class HooksJsonContext : JsonSerializerContext
{
}

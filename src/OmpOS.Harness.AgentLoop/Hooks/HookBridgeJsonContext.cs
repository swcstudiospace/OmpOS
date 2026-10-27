using System.Text.Json;
using System.Text.Json.Serialization;

namespace OmpOS.Harness.AgentLoop.Hooks;

/// <summary>
/// Source-generated JSON metadata for the hook bridge. Reflection-based serialization is not used. Names are camelCase for the
/// request and the startup file; <see cref="BridgeDecision"/> names every member itself, in snake_case, as the extension
/// expects. Reading is tolerant of newer extensions: unmapped members are skipped, and names are matched without regard to
/// case. Null members are not written.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(BridgeRequest))]
[JsonSerializable(typeof(BridgeDecision))]
[JsonSerializable(typeof(HookBridgeStartupFile))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class HookBridgeJsonContext : JsonSerializerContext
{
}

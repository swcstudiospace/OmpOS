using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.AgentLoop.Steering;

/// <summary>
/// Source-generated JSON metadata for the steering files. Reflection-based serialization is not used. Property names are fixed
/// on the wire types. Unmapped members are skipped, because the plugin owns the acknowledgement and heartbeat files and may
/// add members.
/// </summary>
[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(SteeringDirectiveWire))]
[JsonSerializable(typeof(SteeringAckWire))]
[JsonSerializable(typeof(SteeringAliveWire))]
internal sealed partial class SteeringJsonContext : JsonSerializerContext
{
}

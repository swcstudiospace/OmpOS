using System.Text.Json.Serialization;
using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.AgentLoop.Usage;

/// <summary>
/// Source-generated JSON for one ledger line. Unmapped members are refused, so a line that carries prompt content cannot be read as a row.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UsageLedgerEntry))]
[JsonSerializable(typeof(UsageCost))]
internal sealed partial class UsageJsonContext : JsonSerializerContext
{
}

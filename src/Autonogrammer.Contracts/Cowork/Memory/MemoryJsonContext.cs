using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Cowork.Memory;

/// <summary>Source-generated JSON for memory records. Camel case, nulls omitted. No account id is serialised.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MemoryEntryRecord))]
[JsonSerializable(typeof(MemorySaveRequest))]
[JsonSerializable(typeof(MemoryRecallRequest))]
[JsonSerializable(typeof(MemoryRecallResponse))]
[JsonSerializable(typeof(MemorySettingsRecord))]
[JsonSerializable(typeof(MemoryExportDocument))]
[JsonSerializable(typeof(MemoryErrorRecord))]
[JsonSerializable(typeof(List<MemoryEntryRecord>))]
public sealed partial class MemoryJsonContext : JsonSerializerContext
{
}

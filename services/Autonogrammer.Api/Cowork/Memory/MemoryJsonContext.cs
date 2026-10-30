using System.Text.Json.Serialization;
using Autonogrammer.Contracts.Cowork.Memory;

namespace Autonogrammer.Api.Cowork.Memory;

/// <summary>Source-generated JSON for memory routes. Camel case, nulls omitted. No account id is written.</summary>
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
internal sealed partial class MemoryJsonContext : JsonSerializerContext
{
}

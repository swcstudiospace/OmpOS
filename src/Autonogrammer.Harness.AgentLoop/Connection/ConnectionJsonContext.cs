using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.AgentLoop.Connection;

/// <summary>
/// Source-generated JSON metadata for the connection report. Reflection-based serialization is not used. The report belongs to
/// the omp plugin, so reading is tolerant: unmapped members are skipped, names are matched without regard to case, and trailing
/// commas and comments do not fail a parse.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    AllowTrailingCommas = true,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(PluginConnectionDocument))]
internal sealed partial class ConnectionJsonContext : JsonSerializerContext
{
}

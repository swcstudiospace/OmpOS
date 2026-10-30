using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Sessions;

internal sealed class CoworkProjectDocument
{
    public int SchemaVersion { get; set; } = 1;

    public List<CoworkProjectEntry>? Projects { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

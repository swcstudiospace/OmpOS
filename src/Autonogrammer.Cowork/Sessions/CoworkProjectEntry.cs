using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Sessions;

internal sealed class CoworkProjectEntry
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Folder { get; set; }

    public string? Instructions { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Sessions;

internal sealed class CoworkSessionMetaDocument
{
    public int SchemaVersion { get; set; } = 1;

    public Dictionary<string, CoworkSessionMetaEntry>? Sessions { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

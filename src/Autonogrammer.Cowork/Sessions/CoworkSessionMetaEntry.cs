using System.Text.Json;
using System.Text.Json.Serialization;

namespace Autonogrammer.Cowork.Sessions;

internal sealed class CoworkSessionMetaEntry
{
    public string? ProjectId { get; set; }

    public List<string>? TrustedFolders { get; set; }

    public bool MemoryOff { get; set; }

    public List<string>? PinnedArtifactIds { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

using System.Text.Json.Serialization;

namespace Autonogrammer.Sandbox;

/// <summary>
/// A compose volume mount.
/// </summary>
public sealed class ComposeVolume
{
    /// <summary>Gets or sets the source (volume name or host path).</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the target path inside the container.</summary>
    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the mount is read-only.</summary>
    [JsonPropertyName("read_only")]
    public bool ReadOnly { get; set; }

    /// <summary>Gets or sets the mount type (bind, volume, tmpfs).</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;
}

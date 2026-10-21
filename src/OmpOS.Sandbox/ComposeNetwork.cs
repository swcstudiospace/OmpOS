using System.Text.Json.Serialization;

namespace OmpOS.Sandbox;

/// <summary>
/// A compose network definition.
/// </summary>
public sealed class ComposeNetwork
{
    /// <summary>Gets or sets whether the network is internal (no outbound route).</summary>
    [JsonPropertyName("internal")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Internal { get; set; }

    /// <summary>Gets or sets the network driver.</summary>
    [JsonPropertyName("driver")]
    public string Driver { get; set; } = "bridge";
}

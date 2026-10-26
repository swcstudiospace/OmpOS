using System.Text.Json.Serialization;

namespace OmpOS.Harness.AgentLoop.Steering;

/// <summary>The directive file the plugin reads (<c>inbox/&lt;id&gt;.json</c>), schema v1.</summary>
internal sealed class SteeringDirectiveWire
{
    /// <summary>Gets or sets the schema version.</summary>
    [JsonPropertyName("v")]
    public int Version { get; set; }

    /// <summary>Gets or sets the identifier, which equals the file name without its extension.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the delivery mode: <c>steer</c>, <c>followUp</c> or <c>aside</c>.</summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = string.Empty;

    /// <summary>Gets or sets the message.</summary>
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the creation time, ISO 8601.</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>Gets or sets the origin of the directive, always <c>ompos</c>.</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;
}

using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.AgentLoop.Steering;

/// <summary>The acknowledgement file the plugin writes (<c>acks/&lt;id&gt;.json</c>), schema v1. Every member is nullable: the reader decides what is usable.</summary>
internal sealed class SteeringAckWire
{
    /// <summary>Gets or sets the schema version.</summary>
    [JsonPropertyName("v")]
    public int? Version { get; set; }

    /// <summary>Gets or sets the identifier of the directive.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the status: <c>delivered</c>, <c>rejected</c> or <c>failed</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>Gets or sets the reason code, or null.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>Gets or sets the delivery mode, or null.</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    /// <summary>Gets or sets the time of the acknowledgement, ISO 8601.</summary>
    [JsonPropertyName("at")]
    public string? At { get; set; }

    /// <summary>Gets or sets whether the session was idle at delivery, or null.</summary>
    [JsonPropertyName("idle")]
    public bool? Idle { get; set; }
}

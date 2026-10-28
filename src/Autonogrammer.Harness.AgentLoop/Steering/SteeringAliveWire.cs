using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.AgentLoop.Steering;

/// <summary>The heartbeat file the plugin refreshes every five seconds (<c>alive.json</c>), schema v1. Every member is nullable: the reader decides what is usable.</summary>
internal sealed class SteeringAliveWire
{
    /// <summary>Gets or sets the schema version.</summary>
    [JsonPropertyName("v")]
    public int? Version { get; set; }

    /// <summary>Gets or sets the process identifier of the omp process.</summary>
    [JsonPropertyName("pid")]
    public long? Pid { get; set; }

    /// <summary>Gets or sets the start time of the plugin, ISO 8601.</summary>
    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; set; }

    /// <summary>Gets or sets the time of the last refresh, ISO 8601.</summary>
    [JsonPropertyName("lastSeen")]
    public string? LastSeen { get; set; }

    /// <summary>Gets or sets the omp run mode.</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
}

using System.Text.Json.Serialization;

namespace OmpOS.Harness.AgentLoop.Connection;

/// <summary>
/// The connection report file as the omp plugin writes it. Everything is nullable and nothing is required: the file belongs to
/// another process, so unknown members are skipped and a missing member means "not there", never a failure.
/// </summary>
internal sealed class PluginConnectionDocument
{
    /// <summary>Gets or sets the schema version.</summary>
    [JsonPropertyName("v")]
    public int? Version { get; set; }

    /// <summary>Gets or sets the time the report was written, as the plugin wrote it.</summary>
    [JsonPropertyName("at")]
    public string? At { get; set; }

    /// <summary>Gets or sets the process identifier of the omp process.</summary>
    [JsonPropertyName("pid")]
    public long? Pid { get; set; }

    /// <summary>Gets or sets the mode omp runs in.</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    /// <summary>Gets or sets what the report says about omp itself.</summary>
    [JsonPropertyName("omp")]
    public OmpPart? Omp { get; set; }

    /// <summary>Gets or sets the names of the tools omp lists.</summary>
    [JsonPropertyName("tools")]
    public List<string?>? Tools { get; set; }

    /// <summary>Gets or sets the names of the slash commands omp lists.</summary>
    [JsonPropertyName("commands")]
    public List<string?>? Commands { get; set; }

    /// <summary>Gets or sets a value saying whether omp could list its commands.</summary>
    [JsonPropertyName("commandsAvailable")]
    public bool? CommandsAvailable { get; set; }

    /// <summary>What a report says about omp itself.</summary>
    internal sealed class OmpPart
    {
        /// <summary>Gets or sets the omp version.</summary>
        [JsonPropertyName("version")]
        public string? Version { get; set; }
    }
}

using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>
/// The members of <c>live.json</c> (schema <c>swarm.live.v1</c>) that the swarm observer reads. Everything is nullable and
/// unknown members are skipped. Free-text members (<c>activity</c>, <c>last_tool</c>, <c>error</c> text) are deliberately not
/// mapped: the panel shows ids, states and counters only.
/// </summary>
internal sealed class SwarmLiveDocument
{
    /// <summary>The schema identifier this build understands.</summary>
    public const string SchemaId = "swarm.live.v1";

    /// <summary>Gets or sets the schema identifier.</summary>
    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    /// <summary>Gets or sets the runs, at most 64 entries.</summary>
    [JsonPropertyName("runs")]
    public List<RunPart?>? Runs { get; set; }

    /// <summary>One subagent run.</summary>
    internal sealed class RunPart
    {
        /// <summary>Gets or sets the run identifier, <c>&lt;task_id&gt;.a&lt;attempt&gt;</c>.</summary>
        [JsonPropertyName("run_id")]
        public string? RunId { get; set; }

        /// <summary>Gets or sets the agent that runs, for example <c>a05-backend</c>.</summary>
        [JsonPropertyName("agent")]
        public string? Agent { get; set; }

        /// <summary>Gets or sets the run state from the schema vocabulary.</summary>
        [JsonPropertyName("state")]
        public string? State { get; set; }

        /// <summary>Gets or sets a value saying the run is running but silent.</summary>
        [JsonPropertyName("stalled")]
        public bool? Stalled { get; set; }

        /// <summary>Gets or sets the error text or code of a failed run. Only an error code is ever shown.</summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}

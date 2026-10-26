using System.Text.Json.Serialization;

namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>
/// The members of one <c>events.jsonl</c> line that the swarm observer reads: the event type and the identifiers and state
/// words of the run or subagent it concerns. Everything is nullable and unknown members are skipped.
/// </summary>
internal sealed class SwarmEventRecord
{
    /// <summary>Gets or sets the event type, for example <c>swarm.run.state</c> or <c>subagent.spawn</c>.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>Gets or sets the event payload.</summary>
    [JsonPropertyName("payload")]
    public PayloadPart? Payload { get; set; }

    /// <summary>The payload members that name a run and its state.</summary>
    internal sealed class PayloadPart
    {
        /// <summary>Gets or sets the run identifier.</summary>
        [JsonPropertyName("run_id")]
        public string? RunId { get; set; }

        /// <summary>Gets or sets the subagent identifier of a <c>subagent.*</c> event.</summary>
        [JsonPropertyName("subagent_id")]
        public string? SubagentId { get; set; }

        /// <summary>Gets or sets the agent that runs.</summary>
        [JsonPropertyName("agent")]
        public string? Agent { get; set; }

        /// <summary>Gets or sets the state named by a finished or heartbeat event.</summary>
        [JsonPropertyName("state")]
        public string? State { get; set; }

        /// <summary>Gets or sets the state a run moved to.</summary>
        [JsonPropertyName("to")]
        public string? To { get; set; }

        /// <summary>Gets or sets the status word of a <c>subagent.status.stream</c> event.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        /// <summary>Gets or sets a value saying the run is running but silent.</summary>
        [JsonPropertyName("stalled")]
        public bool? Stalled { get; set; }

        /// <summary>Gets or sets the error code of a finished run.</summary>
        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }
    }
}

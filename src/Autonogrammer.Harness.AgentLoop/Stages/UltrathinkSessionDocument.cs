using System.Text.Json.Serialization;

namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>
/// The members of an Ultrathink session record (<c>sessions/&lt;id&gt;.json</c>) that the plan observer reads. Everything is
/// nullable and nothing is required: the file belongs to a vendor, so unknown members are skipped and a missing member means
/// "not there", never a failure. Only the members that are read are mapped, so a change to any other member cannot break the
/// parse.
/// </summary>
internal sealed class UltrathinkSessionDocument
{
    /// <summary>Gets or sets the planner outcome.</summary>
    [JsonPropertyName("result")]
    public ResultPart? Result { get; set; }

    /// <summary>Gets or sets the Graph of Thought.</summary>
    [JsonPropertyName("graph")]
    public GraphPart? Graph { get; set; }

    /// <summary>Gets or sets the clarification questions the planner raised.</summary>
    [JsonPropertyName("clarifications")]
    public List<EmptyPart?>? Clarifications { get; set; }

    /// <summary>Gets or sets the tracking plan. It exists only for a real plan.</summary>
    [JsonPropertyName("plan")]
    public PlanPart? Plan { get; set; }

    /// <summary>Gets or sets the state of the issue-tracker rows.</summary>
    [JsonPropertyName("tracking")]
    public TrackingPart? Tracking { get; set; }

    /// <summary>The planner outcome: whether a model produced the spec or a fallback did.</summary>
    internal sealed class ResultPart
    {
        /// <summary>Gets or sets the origin of the spec: <c>llm</c> or <c>fallback</c>.</summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; }

        /// <summary>Gets or sets the graph copy some records embed in the result.</summary>
        [JsonPropertyName("graph")]
        public GraphPart? Graph { get; set; }
    }

    /// <summary>The Graph of Thought.</summary>
    internal sealed class GraphPart
    {
        /// <summary>Gets or sets the thought nodes.</summary>
        [JsonPropertyName("nodes")]
        public List<NodePart?>? Nodes { get; set; }
    }

    /// <summary>One thought node: its identifier and the nodes it depends on.</summary>
    internal sealed class NodePart
    {
        /// <summary>Gets or sets the node identifier.</summary>
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>Gets or sets the identifiers of the nodes this one depends on.</summary>
        [JsonPropertyName("dependsOn")]
        public List<string?>? DependsOn { get; set; }
    }

    /// <summary>The tracking plan.</summary>
    internal sealed class PlanPart
    {
        /// <summary>Gets or sets the Graph identifier the planner minted.</summary>
        [JsonPropertyName("graphId")]
        public string? GraphId { get; set; }
    }

    /// <summary>The issue-tracker state.</summary>
    internal sealed class TrackingPart
    {
        /// <summary>Gets or sets the tracking status: <c>complete</c>, <c>partial</c> or <c>failed</c>.</summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    /// <summary>An element that is counted and never read.</summary>
    internal sealed class EmptyPart
    {
    }
}

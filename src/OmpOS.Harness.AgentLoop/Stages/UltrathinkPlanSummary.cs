namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>What the plan observer takes from an Ultrathink session record.</summary>
/// <param name="IsFallback">True when the planner wrote a fallback spec, or no tracking plan exists, or the graph is empty: there is no usable plan.</param>
/// <param name="GraphId">The Graph identifier of the plan, or null when it is absent or not a Graph identifier.</param>
/// <param name="NodeCount">The number of thought nodes.</param>
/// <param name="WaveCount">The number of workflow waves: the number of dependency levels of the graph.</param>
/// <param name="ClarificationCount">The number of clarification questions.</param>
/// <param name="TrackingStatus">The tracking status when it is one of <c>complete</c>, <c>partial</c>, <c>failed</c>; otherwise null.</param>
internal sealed record UltrathinkPlanSummary(
    bool IsFallback,
    string? GraphId,
    int NodeCount,
    int WaveCount,
    int ClarificationCount,
    string? TrackingStatus);

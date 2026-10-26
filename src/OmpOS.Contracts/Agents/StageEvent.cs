namespace OmpOS.Contracts.Agents;

/// <summary>
/// One observation about a stage of the agent loop. Producers emit events as plugins make progress; consumers fold them into a
/// timeline keyed by <see cref="SessionId"/>, <see cref="Source"/> and <see cref="StageId"/>. A later event for the same key
/// replaces the earlier one. The event carries plain text only: consumers never interpret any field as markup.
/// </summary>
public sealed record StageEvent
{
    /// <summary>Gets the identifier of the session the stage belongs to.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the component that produced the event.</summary>
    public required StageSource Source { get; init; }

    /// <summary>Gets the stage identifier. It is stable within one session and source, so a later event updates the same row.</summary>
    public required string StageId { get; init; }

    /// <summary>Gets the short title of the stage, for display.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the state of the stage.</summary>
    public required StageStatus Status { get; init; }

    /// <summary>Gets the time the observation was made.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets a short plain-text explanation, or null when the title says everything. For a degraded or failed stage it names the cause.</summary>
    public string? Detail { get; init; }

    /// <summary>Gets the identifier of the parent stage within the same source, or null for a top-level stage. A subagent is a child of the orchestrator stage.</summary>
    public string? ParentStageId { get; init; }

    /// <summary>Gets the Graph identifier the planner minted for the prompt, or null when no plan exists.</summary>
    public string? GraphId { get; init; }
}

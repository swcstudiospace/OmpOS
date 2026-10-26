using OmpOS.Contracts.Agents;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// One row of the Pipeline tab: a stage as the latest event for its key describes it. Every text is plain, sanitised and capped, and
/// the status is always words as well as a glyph key.
/// </summary>
public sealed record StageRow
{
    /// <summary>Gets the component that produced the stage. An undefined value is the generic row of an unknown source.</summary>
    public required StageSource Source { get; init; }

    /// <summary>Gets the stage identifier, unique within the session and source.</summary>
    public required string StageId { get; init; }

    /// <summary>Gets the identifier of the parent stage, or null for a top-level row.</summary>
    public string? ParentStageId { get; init; }

    /// <summary>Gets the nesting depth: 0 for a top-level row, 1 for a child of it, and so on, capped at <see cref="StageTimelineModel.MaxDepth"/>.</summary>
    public required int Depth { get; init; }

    /// <summary>Gets the display title, capped at <see cref="StageTimelineModel.MaxTitleChars"/> characters.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the stage status. An undefined value is kept as it arrived; <see cref="IsStatusKnown"/> is then false.</summary>
    public required StageStatus Status { get; init; }

    /// <summary>Gets whether <see cref="Status"/> is a defined value. An unknown status reads <c>Unknown</c> with the info glyph.</summary>
    public required bool IsStatusKnown { get; init; }

    /// <summary>Gets the status in words: Waiting, Running, Done, Degraded, Failed, Skipped, Unavailable or Unknown.</summary>
    public required string StatusText { get; init; }

    /// <summary>Gets the glyph key of the status, from <see cref="StatusIcon"/>.</summary>
    public required string IconKey { get; init; }

    /// <summary>Gets the plain detail text capped at <see cref="StageTimelineModel.MaxDetailChars"/> characters, or null when there is none.</summary>
    public string? Detail { get; init; }

    /// <summary>Gets what the status means for the run, or null for a status that needs no line.</summary>
    public string? EffectText { get; init; }

    /// <summary>Gets the Graph identifier the planner minted for the prompt, or null.</summary>
    public string? GraphId { get; init; }

    /// <summary>Gets the time of the observation this row shows.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Gets whether the row offers <see cref="RetryName"/>: a failed, degraded or unavailable top-level stage of a source that can be re-checked.</summary>
    public required bool CanRetry { get; init; }

    /// <summary>Gets the accessible name of the retry button: <c>Retry {Title} stage</c>.</summary>
    public required string RetryName { get; init; }

    /// <summary>Gets the accessible name of the row: <c>{Title}, {status text}</c> plus <c>, {Detail}</c> when there is a detail.</summary>
    public required string AccessibleName { get; init; }
}

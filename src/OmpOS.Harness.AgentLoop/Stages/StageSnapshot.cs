using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>
/// The observed state of one stage at one poll, before it is stamped with a session and a time. The observers produce a set
/// of snapshots per poll and <see cref="StageEventDeduper"/> turns the ones that changed into <see cref="StageEvent"/>s.
/// </summary>
/// <param name="StageId">The stage identifier, stable within the source.</param>
/// <param name="Title">The short display title.</param>
/// <param name="Status">The state of the stage.</param>
/// <param name="Detail">The plain-text explanation, already cleaned and truncated, or null.</param>
/// <param name="ParentStageId">The parent stage identifier, or null for a top-level stage.</param>
/// <param name="GraphId">The Graph identifier, or null.</param>
internal sealed record StageSnapshot(
    string StageId,
    string Title,
    StageStatus Status,
    string? Detail,
    string? ParentStageId = null,
    string? GraphId = null);

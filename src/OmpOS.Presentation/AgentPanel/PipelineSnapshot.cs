using OmpOS.Contracts.Agents;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>Everything <see cref="StageTimelineModel"/> shows, derived from its folded entries in one pass.</summary>
/// <param name="Blocks">The Pipeline tab blocks in pipeline order.</param>
/// <param name="Pills">The pipeline strip pills in pipeline order.</param>
/// <param name="SummaryClauses">The run-summary clause of every plugin source that has events, in pipeline order.</param>
/// <param name="DegradedCount">The number of top-level plugin stages that are skipped, degraded, failed or unavailable.</param>
/// <param name="AllPluginsUnavailable">True when every plugin source has events and all its top-level stages are unavailable.</param>
internal sealed record PipelineSnapshot(
    IReadOnlyList<StageSourceBlock> Blocks,
    IReadOnlyList<PipelinePill> Pills,
    IReadOnlyList<KeyValuePair<StageSource, string>> SummaryClauses,
    int DegradedCount,
    bool AllPluginsUnavailable);

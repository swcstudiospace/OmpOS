using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Gsd;

/// <summary>
/// The workflow phases of one workspace as the workflow engine reports them, and the phase the work is at.
/// </summary>
public sealed record GsdWorkflowSnapshot
{
    /// <summary>The status text of a finished phase.</summary>
    public const string CompleteStatus = "complete";

    /// <summary>Gets every phase the roadmap lists, in roadmap order.</summary>
    public required IReadOnlyList<GsdPhaseInfo> Phases { get; init; }

    /// <summary>Gets the phase the work is at, or null when the roadmap lists no phase.</summary>
    public GsdPhaseInfo? CurrentPhase { get; init; }
}

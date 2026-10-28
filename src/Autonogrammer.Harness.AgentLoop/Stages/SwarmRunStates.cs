using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>
/// The mapping table from the swarm state vocabulary to <see cref="SwarmRunPhase"/>, <see cref="StageStatus"/> and a display
/// label. The run vocabulary is the enum of <c>swarm.live.v1</c> (<c>runs[].state</c>); the Task Store words are those of
/// <c>swarm.lifecycle._TASK_TO_RUN</c>, which <c>subagent.status.stream</c> events carry. Lookup ignores case.
/// </summary>
internal static class SwarmRunStates
{
    private static readonly Dictionary<string, SwarmRunPhase> Vocabulary = new(StringComparer.OrdinalIgnoreCase)
    {
        // swarm.live.v1 runs[].state
        ["waiting"] = SwarmRunPhase.Waiting,
        ["queued"] = SwarmRunPhase.Queued,
        ["starting"] = SwarmRunPhase.Starting,
        ["running"] = SwarmRunPhase.Running,
        ["handing_off"] = SwarmRunPhase.HandingOff,
        ["completed"] = SwarmRunPhase.Completed,
        ["failed"] = SwarmRunPhase.Failed,
        ["cancelled"] = SwarmRunPhase.Cancelled,
        ["timed_out"] = SwarmRunPhase.TimedOut,

        // Task Store states (swarm/taskstore.py), as projected by swarm/lifecycle.py from_task_state
        ["CREATED"] = SwarmRunPhase.Waiting,
        ["VALIDATED"] = SwarmRunPhase.Waiting,
        ["CHANGES_REQUESTED"] = SwarmRunPhase.Waiting,
        ["BLOCKED"] = SwarmRunPhase.Waiting,
        ["PLANNED"] = SwarmRunPhase.Queued,
        ["RETRY"] = SwarmRunPhase.Queued,
        ["CLAIMED"] = SwarmRunPhase.Starting,
        ["IN_PROGRESS"] = SwarmRunPhase.Running,
        ["IN_REVIEW"] = SwarmRunPhase.Completed,
        ["APPROVED"] = SwarmRunPhase.Completed,
        ["DONE"] = SwarmRunPhase.Completed,
        ["ESCALATED"] = SwarmRunPhase.Failed,
        ["CANCELED"] = SwarmRunPhase.Cancelled,
    };

    /// <summary>Maps a state word to its phase. A word this build does not know is <see cref="SwarmRunPhase.Unknown"/>.</summary>
    /// <param name="raw">The state word from a vendor file.</param>
    /// <returns>The phase.</returns>
    public static SwarmRunPhase Parse(string? raw) =>
        raw is not null && Vocabulary.TryGetValue(raw.Trim(), out var phase) ? phase : SwarmRunPhase.Unknown;

    /// <summary>Gets a value saying whether a run in this phase will not change again.</summary>
    /// <param name="phase">The phase.</param>
    /// <returns>True for completed, failed, cancelled and timed out.</returns>
    public static bool IsTerminal(SwarmRunPhase phase) =>
        phase is SwarmRunPhase.Completed or SwarmRunPhase.Failed or SwarmRunPhase.Cancelled or SwarmRunPhase.TimedOut;

    /// <summary>
    /// Gets how far along a run is. A state update never moves a run to a lower rank, so a late or replayed event cannot undo
    /// progress that the live file already showed.
    /// </summary>
    /// <param name="phase">The phase.</param>
    /// <returns>The rank, higher for later phases.</returns>
    public static int Rank(SwarmRunPhase phase) => phase switch
    {
        SwarmRunPhase.Waiting => 0,
        SwarmRunPhase.Queued => 1,
        SwarmRunPhase.Starting => 2,
        SwarmRunPhase.Running => 3,
        SwarmRunPhase.Unknown => 3,
        SwarmRunPhase.HandingOff => 4,
        _ => 5,
    };

    /// <summary>Maps a phase to the stage status of its child row.</summary>
    /// <param name="phase">The phase.</param>
    /// <returns>The status.</returns>
    public static StageStatus ToStatus(SwarmRunPhase phase) => phase switch
    {
        SwarmRunPhase.Waiting => StageStatus.Pending,
        SwarmRunPhase.Queued => StageStatus.Pending,
        SwarmRunPhase.Starting => StageStatus.Running,
        SwarmRunPhase.Running => StageStatus.Running,
        SwarmRunPhase.HandingOff => StageStatus.Running,
        SwarmRunPhase.Completed => StageStatus.Succeeded,
        SwarmRunPhase.Failed => StageStatus.Failed,
        SwarmRunPhase.TimedOut => StageStatus.Failed,
        SwarmRunPhase.Cancelled => StageStatus.Skipped,
        _ => StageStatus.Running,
    };

    /// <summary>Gets the display label of a phase. <see cref="SwarmRunPhase.Unknown"/> has no label of its own: the raw word is shown.</summary>
    /// <param name="phase">The phase.</param>
    /// <returns>The label, or null for <see cref="SwarmRunPhase.Unknown"/>.</returns>
    public static string? Label(SwarmRunPhase phase) => phase switch
    {
        SwarmRunPhase.Waiting => "Waiting for dependencies",
        SwarmRunPhase.Queued => "Queued",
        SwarmRunPhase.Starting => "Starting",
        SwarmRunPhase.Running => "Running",
        SwarmRunPhase.HandingOff => "Handing off",
        SwarmRunPhase.Completed => "Completed",
        SwarmRunPhase.Failed => "Failed",
        SwarmRunPhase.Cancelled => "Cancelled",
        SwarmRunPhase.TimedOut => "Timed out",
        _ => null,
    };
}

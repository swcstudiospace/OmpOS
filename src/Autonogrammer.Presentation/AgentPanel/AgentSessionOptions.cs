using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.Approvals;
using Autonogrammer.Presentation.Composer;
using Autonogrammer.Presentation.Runs;
using Autonogrammer.Presentation.State;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// What a head supplies to build the view model of one session: the existing session, composer and scheduler it created for the session's
/// engine, the scope its observers and context source read, and the optional hooks to Substrate, the plugins and the engine lifecycle.
/// </summary>
public sealed class AgentSessionOptions
{
    /// <summary>Gets the rail row of the session, which names it and gives its status.</summary>
    public required AgentRow Row { get; init; }

    /// <summary>Gets the transcript view model of the session.</summary>
    public required SessionViewModel Session { get; init; }

    /// <summary>Gets the composer of the session.</summary>
    public required ComposerViewModel Composer { get; init; }

    /// <summary>Gets the scheduler that routes messages sent while the session's run is active.</summary>
    public required RunInputScheduler Scheduler { get; init; }

    /// <summary>Gets the scope the stage sources and the context source read: only state that belongs to this session.</summary>
    public required StageObservationScope Scope { get; init; }

    /// <summary>Gets the clock. Null uses <see cref="TimeProvider.System"/>.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>Gets the source of the task context, or null when there is none.</summary>
    public ITaskContextSource? TaskContextSource { get; init; }

    /// <summary>Gets the callback that records an accepted steering entry, for example in Substrate, or null when none is bound.</summary>
    public Func<SteeringEntry, CancellationToken, Task>? SteeringRecorder { get; init; }

    /// <summary>
    /// Gets the callback that delivers a steer or a follow-up into the running session at once and reports what the channel can prove, or
    /// null when messages sent during a run are queued for the end of the turn.
    /// </summary>
    public Func<string, SteeringMode, CancellationToken, Task<SteeringDelivery>>? LiveSteering { get; init; }

    /// <summary>Gets the callback that re-checks a plugin source on request, or null when stages offer no retry.</summary>
    public Func<StageSource, CancellationToken, Task>? StageRecheck { get; init; }

    /// <summary>Gets the callback that resumes a lost session by starting its engine again, or null when it cannot be resumed.</summary>
    public Func<CancellationToken, Task>? Resume { get; init; }

    /// <summary>Gets the callback that reopens a closed session by starting its engine again, or null when it cannot be reopened.</summary>
    public Func<CancellationToken, Task>? Reopen { get; init; }

    /// <summary>Gets the coordinator that sends tool-approval decisions for the session, or null when none is bound.</summary>
    public ToolApprovalCoordinator? ApprovalCoordinator { get; init; }
}

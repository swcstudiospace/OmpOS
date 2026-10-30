using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// The single mapping from a session's persisted <see cref="SessionLifecycle"/>, its live <see cref="RuntimeStatus"/> and its closed flag
/// to the status a rail row shows (46-UI-SPEC, Agent Row and Status Chip).
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle to runtime status goes through <see cref="RuntimeStatusMap"/>, never around it (VX-04: status comes from runtime signals):
/// Created is <c>RunQueued</c> (Queued), Running is <c>RunStarted</c> (Running), Paused is <c>RunPaused</c> (Paused), Completed is
/// <c>RunCompleted</c> (Completed), Failed is <c>RunFailed</c> (Failed) and Lost is <c>SessionUnreachable</c> (Unreachable).
/// </para>
/// <para>
/// Precedence: (1) a lifecycle outside <see cref="SessionLifecycle"/> is <see cref="AgentRowStatus.Unknown"/>; (2) while the lifecycle is
/// Created or Running and the head reports a live runtime status, the live status wins, because the engine knows more than the last
/// persisted write (a pending approval, an awaited message); (3) a Paused or Completed session without an engine is Closed; (4) Created
/// with no live status is Ready; (5) otherwise the lifecycle's own runtime status decides. Lost always shows as Lost.
/// </para>
/// <para>
/// Staleness is not a status. <see cref="KanbanCardState.IsStale"/> stays independent: <see cref="SessionCatalogModel"/> sets it for a
/// Running row whose last activity is older than <see cref="SessionCatalogOptions.StaleAfter"/>, and the row keeps its status.
/// </para>
/// </remarks>
public static class AgentRowStatusMap
{
    /// <summary>Resolves the row status of one session.</summary>
    /// <param name="lifecycle">The persisted lifecycle.</param>
    /// <param name="live">The live runtime status the head reports for the session, or null when it reports none.</param>
    /// <param name="isClosed">True when the operator closed the session and no engine is attached.</param>
    /// <returns>The mapping. It never throws: an unknown value is <see cref="AgentRowStatus.Unknown"/>.</returns>
    public static AgentRowMapping Resolve(SessionLifecycle lifecycle, RuntimeStatus? live, bool isClosed)
    {
        if (!Enum.IsDefined(lifecycle))
        {
            return new AgentRowMapping(AgentRowStatus.Unknown, null, AgentRowGroup.Idle);
        }

        var persisted = RuntimeStatusMap.StatusOf(SignalOf(lifecycle));
        if (lifecycle is SessionLifecycle.Created or SessionLifecycle.Running && live is { } liveStatus)
        {
            return FromRuntime(liveStatus);
        }

        if (isClosed && lifecycle is SessionLifecycle.Paused or SessionLifecycle.Completed)
        {
            return new AgentRowMapping(AgentRowStatus.Closed, persisted, AgentRowGroup.Idle);
        }

        return lifecycle == SessionLifecycle.Created
            ? new AgentRowMapping(AgentRowStatus.Ready, persisted, AgentRowGroup.Idle)
            : FromRuntime(persisted);
    }

    /// <summary>Returns the words of a row status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>The status text, for example <c>Needs approval</c>.</returns>
    public static string TextOf(AgentRowStatus status) => status switch
    {
        AgentRowStatus.NeedsApproval => "Needs approval",
        AgentRowStatus.WaitingForYou => "Waiting for you",
        AgentRowStatus.Running => "Running",
        AgentRowStatus.Queued => "Queued",
        AgentRowStatus.Lost => "Lost",
        AgentRowStatus.Ready => "Ready",
        AgentRowStatus.Paused => "Paused",
        AgentRowStatus.Closed => "Closed",
        AgentRowStatus.Completed => "Completed",
        AgentRowStatus.Failed => "Failed",
        _ => "Status unknown",
    };

    /// <summary>Returns the glyph key of a row status.</summary>
    /// <param name="status">The status.</param>
    /// <returns>A key from <see cref="StatusIcon"/>.</returns>
    public static string IconOf(AgentRowStatus status) => status switch
    {
        AgentRowStatus.NeedsApproval => StatusIcon.Alert,
        AgentRowStatus.WaitingForYou => StatusIcon.Bubble,
        AgentRowStatus.Running => StatusIcon.Running,
        AgentRowStatus.Queued => StatusIcon.Clock,
        AgentRowStatus.Lost => StatusIcon.Question,
        AgentRowStatus.Paused => StatusIcon.Paused,
        AgentRowStatus.Closed => StatusIcon.Closed,
        AgentRowStatus.Completed => StatusIcon.Check,
        AgentRowStatus.Failed => StatusIcon.Cross,
        _ => StatusIcon.Ring,
    };

    /// <summary>Returns the header text of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The header: <c>Needs you</c>, <c>Running</c>, <c>Lost</c>, <c>Idle</c> or <c>Finished</c>.</returns>
    public static string HeaderOf(AgentRowGroup group) => group switch
    {
        AgentRowGroup.NeedsYou => "Needs you",
        AgentRowGroup.Running => "Running",
        AgentRowGroup.Lost => "Lost",
        AgentRowGroup.Finished => "Finished",
        _ => "Idle",
    };

    private static RuntimeSignalKind SignalOf(SessionLifecycle lifecycle) => lifecycle switch
    {
        SessionLifecycle.Created => RuntimeSignalKind.RunQueued,
        SessionLifecycle.Running => RuntimeSignalKind.RunStarted,
        SessionLifecycle.Paused => RuntimeSignalKind.RunPaused,
        SessionLifecycle.Completed => RuntimeSignalKind.RunCompleted,
        SessionLifecycle.Failed => RuntimeSignalKind.RunFailed,
        _ => RuntimeSignalKind.SessionUnreachable,
    };

    private static AgentRowMapping FromRuntime(RuntimeStatus status) => status switch
    {
        RuntimeStatus.NeedsAttention => new AgentRowMapping(AgentRowStatus.NeedsApproval, status, AgentRowGroup.NeedsYou),
        RuntimeStatus.AwaitingInput => new AgentRowMapping(AgentRowStatus.WaitingForYou, status, AgentRowGroup.NeedsYou),
        RuntimeStatus.Running => new AgentRowMapping(AgentRowStatus.Running, status, AgentRowGroup.Running),
        RuntimeStatus.Queued => new AgentRowMapping(AgentRowStatus.Queued, status, AgentRowGroup.Running),
        RuntimeStatus.Unreachable => new AgentRowMapping(AgentRowStatus.Lost, status, AgentRowGroup.Lost),
        RuntimeStatus.Paused => new AgentRowMapping(AgentRowStatus.Paused, status, AgentRowGroup.Idle),
        RuntimeStatus.Completed => new AgentRowMapping(AgentRowStatus.Completed, status, AgentRowGroup.Finished),
        RuntimeStatus.Failed => new AgentRowMapping(AgentRowStatus.Failed, status, AgentRowGroup.Finished),
        _ => new AgentRowMapping(AgentRowStatus.Unknown, null, AgentRowGroup.Idle),
    };
}

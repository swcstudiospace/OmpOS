namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>The rail group of a session row. Groups are shown in this order; an empty group is not drawn.</summary>
public enum AgentRowGroup
{
    /// <summary>Sessions that wait for the user: <c>Needs you</c>.</summary>
    NeedsYou = 0,

    /// <summary>Sessions that run or are queued: <c>Running</c>.</summary>
    Running = 1,

    /// <summary>Sessions that lost their engine: <c>Lost</c>.</summary>
    Lost = 2,

    /// <summary>Sessions that are ready, paused, closed or of unknown status: <c>Idle</c>.</summary>
    Idle = 3,

    /// <summary>Sessions whose last run ended: <c>Finished</c>.</summary>
    Finished = 4,
}

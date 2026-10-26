using OmpOS.Contracts.Agents;
using OmpOS.Presentation.Kanban;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// One row of the Agents rail: a session with its status, last activity and flags, ready to draw. Rows are immutable snapshots;
/// <see cref="SessionCatalogModel"/> replaces them when the catalog or the clock changes. Every text is plain, sanitised and capped.
/// </summary>
public sealed record AgentRow
{
    /// <summary>Gets the session the row shows.</summary>
    public required SessionSummary Summary { get; init; }

    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display title. A blank title reads <see cref="AgentPanelCopy.UntitledAgent"/>.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the row status.</summary>
    public required AgentRowStatus Status { get; init; }

    /// <summary>Gets the rail group of the row.</summary>
    public required AgentRowGroup Group { get; init; }

    /// <summary>Gets the Kanban card state: the runtime status and the independent stale flag. Null when the lifecycle is unknown.</summary>
    public KanbanCardState? Card { get; init; }

    /// <summary>Gets the status in words, for example <c>Needs approval</c>.</summary>
    public required string StatusText { get; init; }

    /// <summary>Gets the glyph key of the status, from <see cref="StatusIcon"/>.</summary>
    public required string IconKey { get; init; }

    /// <summary>Gets the relative time of the last activity, for example <c>5 min ago</c>.</summary>
    public required string LastActivityText { get; init; }

    /// <summary>Gets whether the row is Running and its session was created more than <see cref="SessionCatalogOptions.LongRunningAfter"/> ago.</summary>
    public required bool IsLongRunning { get; init; }

    /// <summary>Gets whether the row is Running and its last activity is older than <see cref="SessionCatalogOptions.StaleAfter"/>.</summary>
    public required bool IsStale { get; init; }

    /// <summary>Gets whether the session is lost: its lifecycle is Lost or its live status is unreachable.</summary>
    public required bool IsLost { get; init; }

    /// <summary>Gets <c>Quiet for {n} min</c> for a stale row, which replaces the last-activity text; null otherwise.</summary>
    public string? QuietText { get; init; }

    /// <summary>Gets <c>Task {id}</c>, or <c>Graph {last 8 characters}</c> when only a graph exists, or null.</summary>
    public string? TaskTag { get; init; }

    /// <summary>Gets the task identifier, or null.</summary>
    public string? TaskId { get; init; }

    /// <summary>Gets the Graph identifier, or null.</summary>
    public string? GraphId { get; init; }

    /// <summary>Gets the accessible name: <c>{title}, {status}, {last activity}</c> plus long-running, quiet and task tag parts when present.</summary>
    public required string AccessibleName { get; init; }

    /// <summary>Gets whether the row is the first of its group in the list it belongs to, so the view draws the group header above it.</summary>
    public bool IsGroupStart { get; init; }

    /// <summary>Gets the runtime status of the card, or null when the lifecycle is unknown.</summary>
    public RuntimeStatus? RuntimeStatus => Card?.Status;

    /// <summary>Gets the group header text, for example <c>Needs you</c>.</summary>
    public string GroupHeader => AgentRowStatusMap.HeaderOf(Group);
}

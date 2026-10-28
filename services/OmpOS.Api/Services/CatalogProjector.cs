using OmpOS.Api.Cloud;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Sessions;

namespace OmpOS.Api.Services;

/// <summary>
/// Maps a stored session to the rows the catalog, the board and the rail show. The mapping follows the desktop's row-status map: a
/// running session whose run lease has lapsed is Lost, a pending tool approval needs the user, a finished turn is Completed or Failed.
/// </summary>
internal static class CatalogProjector
{
    /// <summary>How long a session may show as running without any activity before it shows as lost. It is twice the run lease lifetime.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);

    /// <summary>The key of the backlog column.</summary>
    public const string BacklogKey = "backlog";

    /// <summary>The key of the archived column.</summary>
    public const string ArchivedKey = "archived";

    private static readonly (string Key, string Title, bool AcceptsDrop)[] ColumnTable =
    [
        (BacklogKey, "Backlog", true),
        ("queued", "Queued", false),
        ("running", "Running", false),
        ("awaiting-input", "Awaiting input", false),
        ("needs-attention", "Needs attention", false),
        ("paused", "Paused", false),
        ("completed", "Completed", false),
        ("failed", "Failed", false),
        ("unreachable", "Unreachable", false),
        (ArchivedKey, "Archived", true),
    ];

    /// <summary>Gets the board columns in display order.</summary>
    public static IReadOnlyList<(string Key, string Title, bool AcceptsDrop)> Columns => ColumnTable;

    /// <summary>Gets the time of the last activity: the later of the session update and its last logged event.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The last activity time.</returns>
    public static DateTimeOffset LastActivity(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var last = session.Events.Count > 0 ? session.Events[^1].Timestamp : session.UpdatedAt;
        return last > session.UpdatedAt ? last : session.UpdatedAt;
    }

    /// <summary>Gets whether a session shows as running but has had no activity for longer than <see cref="StaleAfter"/>.</summary>
    /// <param name="session">The session.</param>
    /// <param name="now">The current time.</param>
    /// <returns>True for a stale running session.</returns>
    public static bool IsStale(Session session, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Status is SessionStatus.Running or SessionStatus.AwaitingToolApproval && now - LastActivity(session) > StaleAfter;
    }

    /// <summary>Resolves the status and the group the rail shows for a session.</summary>
    /// <param name="session">The session.</param>
    /// <param name="closed">Whether the operator closed the session.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The row status and its group.</returns>
    public static (CatalogRowStatus Status, CatalogRowGroup Group) RowOf(Session session, bool closed, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        var active = session.Status is SessionStatus.Running or SessionStatus.AwaitingToolApproval;
        if (closed && !active)
        {
            return (CatalogRowStatus.Closed, CatalogRowGroup.Idle);
        }

        if (IsStale(session, now))
        {
            return (CatalogRowStatus.Lost, CatalogRowGroup.Lost);
        }

        return session.Status switch
        {
            SessionStatus.Idle => (CatalogRowStatus.Ready, CatalogRowGroup.Idle),
            SessionStatus.Running => (CatalogRowStatus.Running, CatalogRowGroup.Running),
            SessionStatus.AwaitingToolApproval => (CatalogRowStatus.NeedsApproval, CatalogRowGroup.NeedsYou),
            SessionStatus.Completed => (CatalogRowStatus.Completed, CatalogRowGroup.Finished),
            SessionStatus.Failed => (CatalogRowStatus.Failed, CatalogRowGroup.Finished),
            SessionStatus.Cancelled => (CatalogRowStatus.Paused, CatalogRowGroup.Idle),
            _ => (CatalogRowStatus.Unknown, CatalogRowGroup.Idle),
        };
    }

    /// <summary>Resolves the board column a row status sits in.</summary>
    /// <param name="status">The row status.</param>
    /// <returns>The column key.</returns>
    public static string ColumnOf(CatalogRowStatus status) => status switch
    {
        CatalogRowStatus.Queued => "queued",
        CatalogRowStatus.Running => "running",
        CatalogRowStatus.WaitingForYou => "awaiting-input",
        CatalogRowStatus.NeedsApproval => "needs-attention",
        CatalogRowStatus.Paused => "paused",
        CatalogRowStatus.Completed => "completed",
        CatalogRowStatus.Failed => "failed",
        CatalogRowStatus.Lost => "unreachable",
        CatalogRowStatus.Closed => ArchivedKey,
        _ => BacklogKey,
    };

    /// <summary>Builds the catalog row of a session.</summary>
    /// <param name="session">The session.</param>
    /// <param name="closed">Whether the operator closed the session.</param>
    /// <param name="now">The current time.</param>
    /// <param name="graphId">The Graph identifier a stage source reported, or null.</param>
    /// <param name="workspaceLabel">The workspace folder name, or null.</param>
    /// <returns>The row.</returns>
    public static SessionCatalogEntry ToEntry(Session session, bool closed, DateTimeOffset now, string? graphId, string? workspaceLabel)
    {
        ArgumentNullException.ThrowIfNull(session);
        var (status, group) = RowOf(session, closed, now);
        return new SessionCatalogEntry
        {
            Id = session.Id,
            OwnerId = session.OwnerId,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            Status = session.Status,
            SandboxId = session.SandboxId,
            Messages = session.Messages,
            Events = session.Events,
            Kind = CloudSessionIds.IsCloud(session.Id) ? CatalogSessionKind.Cloud : CatalogSessionKind.Agent,
            RowStatus = status,
            Group = group,
            LastActivityAt = LastActivity(session),
            GraphId = graphId,
            Closed = closed,
            WorkspaceLabel = workspaceLabel,
        };
    }

    /// <summary>Builds the title of a card. It is derived from the identifier only, so a card never shows a prompt.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The title.</returns>
    public static string TitleOf(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return $"Agent {(sessionId.Length > 8 ? sessionId[..8] : sessionId)}";
    }
}

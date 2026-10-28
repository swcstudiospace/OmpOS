using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Sessions;

namespace Autonogrammer.Api.Services;

/// <summary>Derives the stages the API can observe without a plugin: the engine turn from the stored session, and the explained absence of each plugin source.</summary>
internal static class StageProjection
{
    /// <summary>The stage identifier of the engine turn.</summary>
    public const string TurnStageId = "turn";

    /// <summary>The stage identifier of a plugin source that is not connected.</summary>
    public const string ConnectionStageId = "connection";

    /// <summary>Derives the engine stage of a session.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The engine stage event.</returns>
    public static StageEvent OmpTurn(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var (status, detail) = session.Status switch
        {
            SessionStatus.Running => (StageStatus.Running, (string?)null),
            SessionStatus.AwaitingToolApproval => (StageStatus.Running, "Waiting for a tool approval."),
            SessionStatus.Completed => (StageStatus.Succeeded, null),
            SessionStatus.Failed => (StageStatus.Failed, "The last turn failed."),
            SessionStatus.Cancelled => (StageStatus.Skipped, "The last turn was cancelled."),
            _ => (StageStatus.Pending, null),
        };
        return new StageEvent
        {
            SessionId = session.Id,
            Source = StageSource.Omp,
            StageId = TurnStageId,
            Title = "Engine turn",
            Status = status,
            Timestamp = CatalogProjector.LastActivity(session),
            Detail = detail,
        };
    }

    /// <summary>Builds the stage that explains why a plugin source shows nothing.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="source">The plugin source.</param>
    /// <param name="now">The current time.</param>
    /// <returns>An unavailable stage event.</returns>
    public static StageEvent NotConnected(string sessionId, StageSource source, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return new StageEvent
        {
            SessionId = sessionId,
            Source = source,
            StageId = ConnectionStageId,
            Title = TitleOf(source),
            Status = StageStatus.Unavailable,
            Timestamp = now,
            Detail = "No stage source is connected to this API for this session.",
        };
    }

    private static string TitleOf(StageSource source) => source switch
    {
        StageSource.Ultrathink => "Planner",
        StageSource.Swarm => "Orchestrator",
        StageSource.Substrate => "Substrate",
        StageSource.Gsd => "Workflow engine",
        _ => "Engine",
    };
}

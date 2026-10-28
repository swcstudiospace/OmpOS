using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>Stage events for the stage tests.</summary>
internal static class StageFixtures
{
    /// <summary>Builds a running planner stage that names a Graph.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="graphId">The Graph identifier.</param>
    /// <returns>The stage event.</returns>
    public static StageEvent Planner(string sessionId, string graphId) => new()
    {
        SessionId = sessionId,
        Source = StageSource.Ultrathink,
        StageId = "plan",
        Title = "Plan",
        Status = StageStatus.Running,
        Timestamp = new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero),
        GraphId = graphId,
    };
}

using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Gsd;

namespace Autonogrammer.Harness.AgentLoop.Tests.Gsd;

/// <summary>
/// Builds the clients and scopes the observer and task-context tests share.
/// </summary>
internal static class GsdTestSupport
{
    /// <summary>Creates a client over <paramref name="runner"/> whose locator finds a fake install inside <paramref name="directory"/>.</summary>
    public static GsdToolsClient NewClient(GsdTempDirectory directory, IGsdProcessRunner runner, TimeProvider? time = null) =>
        new(runner, new GsdToolsLocator(directory.CreateToolsHome("tools")), time ?? new GsdManualTimeProvider());

    /// <summary>Creates a client whose locator finds nothing.</summary>
    public static GsdToolsClient NewClientWithoutTools(GsdTempDirectory directory, IGsdProcessRunner runner, TimeProvider? time = null) =>
        new(runner, new GsdToolsLocator(directory.PathOf("absent-tools")), time ?? new GsdManualTimeProvider());

    /// <summary>Creates an observation scope for a workspace root.</summary>
    public static StageObservationScope Scope(string workspaceRoot, string sessionId = "session-1", string? graphId = null) =>
        new()
        {
            SessionId = sessionId,
            WorkspaceRoot = workspaceRoot,
            SessionStateDirectory = Path.Combine(workspaceRoot, ".session-state"),
            GraphId = graphId,
        };
}

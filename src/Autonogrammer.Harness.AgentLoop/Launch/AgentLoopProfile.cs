namespace Autonogrammer.Harness.AgentLoop.Launch;

/// <summary>
/// How much of the user's own omp and plugin configuration a launched session sees.
/// </summary>
public enum AgentLoopProfile
{
    /// <summary>The user's own configuration applies, as it does in the omp terminal UI.</summary>
    Inherit,

    /// <summary>
    /// The session neutralises the planner's trackers, decisions, memory services and swarm auto-run, so nothing in the
    /// user's home configuration reaches an external service.
    /// </summary>
    Isolated,
}

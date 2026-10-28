using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Plugins;

/// <summary>
/// Builds the stage events that report on a plugin's availability. The planner and <see cref="PluginAvailabilityStageSource"/>
/// use the same stage identifier, so the later event replaces the earlier row.
/// </summary>
internal static class PluginStageEvents
{
    /// <summary>The stage identifier of a plugin's availability row. It is stable per session and source.</summary>
    internal const string StageId = "plugin";

    /// <summary>
    /// Creates the event for one plugin.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <param name="sessionId">The session the event belongs to.</param>
    /// <param name="graphId">The Graph identifier of the session's plan, or null.</param>
    /// <param name="status">The stage state.</param>
    /// <param name="detail">The plain-text explanation.</param>
    /// <param name="timestamp">The time of the observation.</param>
    internal static StageEvent Create(
        OmpPluginDescriptor plugin,
        string sessionId,
        string? graphId,
        StageStatus status,
        string detail,
        DateTimeOffset timestamp) =>
        new()
        {
            SessionId = sessionId,
            Source = plugin.StageSource,
            StageId = StageId,
            Title = plugin.DisplayName + " plugin",
            Status = status,
            Timestamp = timestamp,
            Detail = detail,
            GraphId = graphId,
        };

    /// <summary>
    /// Maps a health to the stage state that reports it: a missing submodule is unavailable, a plugin that is there but
    /// cannot be loaded as it should is degraded.
    /// </summary>
    /// <param name="health">The health of a plugin that is not ready.</param>
    internal static StageStatus StatusFor(PluginHealth health) => health switch
    {
        PluginHealth.Ready => StageStatus.Succeeded,
        PluginHealth.Disabled => StageStatus.Skipped,
        PluginHealth.EntryMissing or PluginHealth.ManifestInvalid => StageStatus.Degraded,
        _ => StageStatus.Unavailable,
    };
}

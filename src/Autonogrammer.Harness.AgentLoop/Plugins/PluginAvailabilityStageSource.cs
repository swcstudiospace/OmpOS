using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.AgentLoop.Launch;

namespace Autonogrammer.Harness.AgentLoop.Plugins;

/// <summary>
/// Reports plugins that are switched on but cannot be loaded. It emits one event per such plugin, with the health message as
/// the detail, and then completes: nothing for a ready plugin and nothing for a plugin the settings switch off. A missing
/// submodule is <see cref="StageStatus.Unavailable"/>; a plugin whose directory is there but whose entry is missing or whose
/// manifest is invalid is <see cref="StageStatus.Degraded"/>.
/// </summary>
public sealed class PluginAvailabilityStageSource : IStageSource
{
    private readonly IReadOnlyList<ResolvedPlugin> _plugins;
    private readonly AgentLoopSettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="plugins">The resolved plugins.</param>
    /// <param name="settings">What the user asked of the agent loop. It says which plugins are switched on.</param>
    /// <param name="timeProvider">The clock that stamps the events. The system clock when null.</param>
    public PluginAvailabilityStageSource(IReadOnlyList<ResolvedPlugin> plugins, AgentLoopSettings settings, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(plugins);
        ArgumentNullException.ThrowIfNull(settings);
        _plugins = plugins;
        _settings = settings;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(
        StageObservationScope scope,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // The events are computed from values already in memory; the await keeps the method a genuine async iterator.
        await Task.CompletedTask.ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        foreach (var plugin in _plugins)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            if (plugin.Health is PluginHealth.Ready or PluginHealth.Disabled || !_settings.IsEnabled(plugin.Descriptor.Id))
            {
                continue;
            }

            yield return PluginStageEvents.Create(
                plugin.Descriptor,
                scope.SessionId,
                scope.GraphId,
                PluginStageEvents.StatusFor(plugin.Health),
                plugin.Message,
                now);
        }
    }
}

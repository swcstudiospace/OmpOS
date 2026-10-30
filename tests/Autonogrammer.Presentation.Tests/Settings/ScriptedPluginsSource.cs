using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Presentation.Tests.Settings;

/// <summary>A Plugins source the test scripts: it reports the rows it holds, or fails the load.</summary>
internal sealed class ScriptedPluginsSource : IPluginsSettingsSource
{
    /// <summary>Gets the four plugins in composition order, as a head reports them.</summary>
    internal static IReadOnlyList<PluginRow> FourPlugins { get; } =
    [
        new("ultrathink", "Ultrathink", "Prompt planner", "1.4.0", PluginRow.FoundAvailability, "claude-ultrathink"),
        new("swarm", "Agent Swarm", "Subagent orchestrator", "0.9.2", PluginRow.FoundAvailability, null),
        new("substrate", "Agent Substrate", "Session substrate", null, PluginRow.MissingAvailability, "submodule not initialised"),
        new("gsd", "GSD", "Workflow engine", "2.0.0", PluginRow.FoundAvailability, null),
    ];

    /// <summary>Gets or sets the rows the source reports.</summary>
    internal IReadOnlyList<PluginRow> Rows { get; set; } = FourPlugins;

    /// <summary>Gets or sets the reason loads fail with, or null when they succeed.</summary>
    internal string? LoadFailure { get; set; }

    /// <inheritdoc/>
    public Task<PluginsSettingsValues> LoadAsync(CancellationToken cancellationToken) =>
        LoadFailure is { } failure
            ? Task.FromException<PluginsSettingsValues>(new SettingsSourceException(failure))
            : Task.FromResult(new PluginsSettingsValues(Rows));
}

using Autonogrammer.Contracts.Catalog;

namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>The answer of <c>GET /settings/plugins</c>: the workspace plugin check and the plugin inventory the API has.</summary>
public sealed record PluginsSettingsResponse
{
    /// <summary>Gets the plugin check result. Its status is <c>unavailable</c> when the API has no plugin inventory.</summary>
    public required PluginCheckResult Check { get; init; }

    /// <summary>Gets the plugins in composition order; empty when the API has no inventory.</summary>
    public required IReadOnlyList<WorkspacePluginInfo> Plugins { get; init; }
}

using Autonogrammer.Contracts.WorkspaceSettings;

namespace Autonogrammer.Api.Services.WorkspaceSettings;

/// <summary>Reads the plugin connection check and the plugin inventory. Both are seams the host fills; the defaults say unavailable and list nothing.</summary>
internal sealed class PluginsSettingsReader
{
    private readonly IWorkspacePluginCheck _check;
    private readonly IWorkspacePluginInventory _inventory;

    /// <summary>Initializes a new instance of the <see cref="PluginsSettingsReader"/> class.</summary>
    /// <param name="check">The plugin connection check.</param>
    /// <param name="inventory">The plugin inventory.</param>
    public PluginsSettingsReader(IWorkspacePluginCheck check, IWorkspacePluginInventory inventory)
    {
        _check = check;
        _inventory = inventory;
    }

    /// <summary>Runs the check and lists the plugins.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The check result and the plugins.</returns>
    public async Task<PluginsSettingsResponse> ReadAsync(CancellationToken cancellationToken)
    {
        var check = await _check.CheckAsync(cancellationToken).ConfigureAwait(false);
        var plugins = await _inventory.ListAsync(cancellationToken).ConfigureAwait(false);
        return new PluginsSettingsResponse { Check = check, Plugins = plugins };
    }
}

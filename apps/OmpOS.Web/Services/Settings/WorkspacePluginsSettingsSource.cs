using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.WorkspaceSettings;
using OmpOS.Presentation.Settings;

namespace OmpOS.Web.Services.Settings;

/// <summary>
/// The Plugins page over the API: the plugin inventory the API reports. An API that has no inventory and cannot run the plugin check is the page's
/// unavailable state with the API's own reason, not an empty list that reads as "no plugins".
/// </summary>
internal sealed class WorkspacePluginsSettingsSource : IPluginsSettingsSource
{
    private readonly WorkspaceSettingsClient _client;

    /// <summary>Initializes a new instance of the <see cref="WorkspacePluginsSettingsSource"/> class.</summary>
    /// <param name="client">The settings client.</param>
    internal WorkspacePluginsSettingsSource(WorkspaceSettingsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc/>
    /// <exception cref="SettingsSourceException">The plugins cannot be read; the message says why.</exception>
    public async Task<PluginsSettingsValues> LoadAsync(CancellationToken cancellationToken)
    {
        PluginsSettingsResponse response;
        try
        {
            response = await _client.GetPluginsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (WorkspaceSettingsFailure.IsExpected(exception))
        {
            throw new SettingsSourceException(WorkspaceSettingsFailure.Describe(exception), exception);
        }

        if (response.Plugins.Count == 0 && response.Check.Status == PluginCheckStatus.Unavailable)
        {
            throw new SettingsSourceException(response.Check.Summary);
        }

        return new PluginsSettingsValues(
            [.. response.Plugins.Select(static plugin => new PluginRow(
                plugin.Id,
                plugin.DisplayName,
                plugin.Role,
                plugin.Version,
                plugin.Found ? PluginRow.FoundAvailability : PluginRow.MissingAvailability,
                plugin.Detail))]);
    }
}

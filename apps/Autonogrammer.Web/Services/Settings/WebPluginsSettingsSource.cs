using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>The <see cref="IPluginsSettingsSource"/> of the web head: the one plugin check <c>GET /workspace</c> reports, as one row.</summary>
internal sealed class WebPluginsSettingsSource : IPluginsSettingsSource
{
    private readonly IOmpApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="WebPluginsSettingsSource"/> class.</summary>
    /// <param name="api">The API client.</param>
    internal WebPluginsSettingsSource(IOmpApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <inheritdoc />
    public async Task<PluginsSettingsValues> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await _api.GetWorkspaceAsync(cancellationToken).ConfigureAwait(true);
            return new PluginsSettingsValues([Row(workspace.PluginCheck)]);
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            throw new SettingsSourceException(WebFailureText.Describe(fault), fault);
        }
    }

    private static PluginRow Row(PluginCheckResult check) => new(
        "plugin-check",
        "Workspace plugins",
        "Ultrathink, Swarm, Substrate and GSD-Core",
        null,
        check.Status switch
        {
            PluginCheckStatus.Ok => PluginRow.FoundAvailability,
            PluginCheckStatus.Degraded => "Degraded",
            _ => PluginRow.MissingAvailability,
        },
        check.Summary);
}

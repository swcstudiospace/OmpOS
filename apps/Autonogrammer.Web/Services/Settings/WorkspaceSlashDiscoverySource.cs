using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>
/// The slash palette's discovery over the API workspace: custom commands, skills and subagents of the server folder. Each call reads again. A failed
/// read is not an exception: the palette keeps working with its built-in rows and the failure is the one problem sentence of an empty discovery.
/// </summary>
internal sealed class WorkspaceSlashDiscoverySource : ISlashDiscoverySource
{
    private readonly WorkspaceSettingsClient _client;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceSlashDiscoverySource"/> class.</summary>
    /// <param name="client">The settings client.</param>
    internal WorkspaceSlashDiscoverySource(WorkspaceSettingsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc/>
    public async Task<SlashDiscovery> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await WorkspaceConventionMapping.ReadAsync(_client, cancellationToken).ConfigureAwait(false);
            return WorkspaceConventionMapping.ToDiscovery(response);
        }
        catch (SettingsSourceException exception)
        {
            return new SlashDiscovery([], [], [], [exception.Message]);
        }
    }
}

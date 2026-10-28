using Autonogrammer.Presentation.Settings.Customize;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>The Rules page over the API workspace: <c>rules/*.mdc</c>, <c>rules/*.md</c>, <c>AGENTS.md</c>, <c>CLAUDE.md</c> and <c>.cursorrules</c> of the server folder.</summary>
internal sealed class WorkspaceRulesSettingsSource : IRulesSettingsSource
{
    private readonly WorkspaceSettingsClient _client;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceRulesSettingsSource"/> class.</summary>
    /// <param name="client">The settings client.</param>
    internal WorkspaceRulesSettingsSource(WorkspaceSettingsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc/>
    public async Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken) =>
        WorkspaceConventionMapping.ToRulesCatalog(await WorkspaceConventionMapping.ReadAsync(_client, cancellationToken).ConfigureAwait(false));
}

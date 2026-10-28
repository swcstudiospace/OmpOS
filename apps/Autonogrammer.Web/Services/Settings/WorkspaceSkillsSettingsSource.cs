using Autonogrammer.Presentation.Settings.Customize;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>The Skills page over the API workspace: the <c>SKILL.md</c> files of the server folder.</summary>
internal sealed class WorkspaceSkillsSettingsSource : ISkillsSettingsSource
{
    private readonly WorkspaceSettingsClient _client;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceSkillsSettingsSource"/> class.</summary>
    /// <param name="client">The settings client.</param>
    internal WorkspaceSkillsSettingsSource(WorkspaceSettingsClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <inheritdoc/>
    public async Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken) =>
        WorkspaceConventionMapping.ToSkillsCatalog(await WorkspaceConventionMapping.ReadAsync(_client, cancellationToken).ConfigureAwait(false));
}

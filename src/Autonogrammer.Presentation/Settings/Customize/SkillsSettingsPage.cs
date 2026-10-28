namespace Autonogrammer.Presentation.Settings.Customize;

/// <summary>The read-only Skills page: the skills the <see cref="ISkillsSettingsSource"/> lists. It has no edit action.</summary>
public sealed class SkillsSettingsPage : CustomizeCatalogPage
{
    private readonly ISkillsSettingsSource _source;

    /// <summary>Initializes a new instance of the <see cref="SkillsSettingsPage"/> class.</summary>
    /// <param name="source">The source that lists the skills.</param>
    public SkillsSettingsPage(ISkillsSettingsSource source)
        : base(SettingsSection.Skills, CustomizeCopy.SkillsTitle, CustomizeCopy.SkillsIntro, CustomizeCopy.SkillsEmpty)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc/>
    protected override Task<CustomizeCatalog> LoadCatalogAsync(CancellationToken cancellationToken) => _source.LoadAsync(cancellationToken);
}

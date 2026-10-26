namespace OmpOS.Presentation.Settings.Customize;

/// <summary>The read-only Rules page: the rules the <see cref="IRulesSettingsSource"/> lists. It has no edit action.</summary>
public sealed class RulesSettingsPage : CustomizeCatalogPage
{
    private readonly IRulesSettingsSource _source;

    /// <summary>Initializes a new instance of the <see cref="RulesSettingsPage"/> class.</summary>
    /// <param name="source">The source that lists the rules.</param>
    public RulesSettingsPage(IRulesSettingsSource source)
        : base(SettingsSection.Rules, CustomizeCopy.RulesTitle, CustomizeCopy.RulesIntro, CustomizeCopy.RulesEmpty)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc/>
    protected override Task<CustomizeCatalog> LoadCatalogAsync(CancellationToken cancellationToken) => _source.LoadAsync(cancellationToken);
}

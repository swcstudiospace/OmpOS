namespace Autonogrammer.Presentation.Settings.Customize;

/// <summary>The read-only Commands page: the commands the <see cref="ICommandsSettingsSource"/> lists. It has no edit action.</summary>
public sealed class CommandsSettingsPage : CustomizeCatalogPage
{
    private readonly ICommandsSettingsSource _source;

    /// <summary>Initializes a new instance of the <see cref="CommandsSettingsPage"/> class.</summary>
    /// <param name="source">The source that lists the commands.</param>
    public CommandsSettingsPage(ICommandsSettingsSource source)
        : base(SettingsSection.Commands, CustomizeCopy.CommandsTitle, CustomizeCopy.CommandsIntro, CustomizeCopy.CommandsEmpty)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <inheritdoc/>
    protected override Task<CustomizeCatalog> LoadCatalogAsync(CancellationToken cancellationToken) => _source.LoadAsync(cancellationToken);
}

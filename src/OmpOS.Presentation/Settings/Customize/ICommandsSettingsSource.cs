namespace OmpOS.Presentation.Settings.Customize;

/// <summary>Reads the slash commands. <see cref="SlashCommandsSettingsSource"/> implements it over the slash command registry.</summary>
public interface ICommandsSettingsSource
{
    /// <summary>Reads the commands.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The catalogue.</returns>
    /// <exception cref="SettingsSourceException">The commands could not be read.</exception>
    Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken);
}

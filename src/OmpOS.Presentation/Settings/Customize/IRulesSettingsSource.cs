namespace OmpOS.Presentation.Settings.Customize;

/// <summary>Reads the rule files the workspace holds. A head maps the workspace catalogue (the <c>.ompos/rules</c> folder) to entries.</summary>
public interface IRulesSettingsSource
{
    /// <summary>Reads the rules.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The catalogue.</returns>
    /// <exception cref="SettingsSourceException">The rules could not be read.</exception>
    Task<CustomizeCatalog> LoadAsync(CancellationToken cancellationToken);
}

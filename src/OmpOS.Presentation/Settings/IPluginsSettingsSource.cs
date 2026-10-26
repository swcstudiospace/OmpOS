namespace OmpOS.Presentation.Settings;

/// <summary>
/// Where the Plugins page reads the plugins. The page is read-only and there is no switch (OQ-80), so there is no write. An
/// expected failure is reported as <see cref="SettingsSourceException"/>.
/// </summary>
public interface IPluginsSettingsSource
{
    /// <summary>Reads the plugins in composition order.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The plugins.</returns>
    /// <exception cref="SettingsSourceException">The plugins cannot be read.</exception>
    Task<PluginsSettingsValues> LoadAsync(CancellationToken cancellationToken);
}

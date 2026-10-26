namespace OmpOS.Presentation.Settings;

/// <summary>
/// Where the Tab page reads the status of inline completion. The page is read-only, so there is no write. A head adapts its
/// completion settings to this interface; an expected failure is reported as <see cref="SettingsSourceException"/>.
/// </summary>
public interface ITabSettingsSource
{
    /// <summary>Reads the status.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The status.</returns>
    /// <exception cref="SettingsSourceException">The status cannot be read.</exception>
    Task<TabSettingsValues> LoadAsync(CancellationToken cancellationToken);
}

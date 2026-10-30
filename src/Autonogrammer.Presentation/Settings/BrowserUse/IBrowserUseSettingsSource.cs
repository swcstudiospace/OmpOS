namespace Autonogrammer.Presentation.Settings.BrowserUse;

/// <summary>
/// Where the Browser page reads Chromium state, reads and writes options, and acquires a pinned archive.
/// Presentation does not reference the browser harness. An expected failure is a <see cref="SettingsSourceException"/>.
/// </summary>
public interface IBrowserUseSettingsSource
{
    /// <summary>Reads Chromium state, the saved options and the platform manifest row.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be read.</exception>
    Task<BrowserUseSnapshot> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Writes the options. A rejected value is a <see cref="SettingsSourceException"/> whose message names the key and never the value.</summary>
    /// <param name="values">The values to save.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the values are written.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be written.</exception>
    Task SaveAsync(BrowserUseSettingsValues values, CancellationToken cancellationToken);

    /// <summary>
    /// Acquires the pinned archive. The Settings page calls this only from <see cref="BrowserUseAcquisitionDialog.ConfirmAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the acquisition.</param>
    /// <returns>A task that completes when the acquisition finishes.</returns>
    /// <exception cref="SettingsSourceException">The acquisition was refused.</exception>
    Task AcquireAsync(CancellationToken cancellationToken);
}

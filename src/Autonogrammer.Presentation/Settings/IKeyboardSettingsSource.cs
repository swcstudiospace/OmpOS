namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// Where the Keyboard page reads the shortcuts. The page is read-only: remapping is omitted by design, so there is no write. An
/// expected failure is reported as <see cref="SettingsSourceException"/>.
/// </summary>
public interface IKeyboardSettingsSource
{
    /// <summary>Reads the shortcuts the shell handles.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The shortcuts.</returns>
    /// <exception cref="SettingsSourceException">The shortcuts cannot be read.</exception>
    Task<KeyboardShortcutsSnapshot> LoadAsync(CancellationToken cancellationToken);
}

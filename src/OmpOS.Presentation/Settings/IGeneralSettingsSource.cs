namespace OmpOS.Presentation.Settings;

/// <summary>
/// Where the General page reads and writes its values. A head adapts its preferences store to this interface; expected failures
/// are reported as <see cref="SettingsSourceException"/>.
/// </summary>
public interface IGeneralSettingsSource
{
    /// <summary>Reads the saved values.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The saved values.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be read.</exception>
    Task<GeneralSettingsValues> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Writes the values.</summary>
    /// <param name="values">The values to save.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the values are written.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be written.</exception>
    Task SaveAsync(GeneralSettingsValues values, CancellationToken cancellationToken);
}

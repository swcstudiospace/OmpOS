using OmpOS.Presentation.Settings;

namespace OmpOS.Presentation.Safety;

/// <summary>
/// Where the Run mode page reads and writes. A head adapts the safety library to this interface. Expected failures are reported as
/// <see cref="SettingsSourceException"/>. A source writes only OmpOS-owned files (under <c>.ompos</c>), never a Cursor file.
/// </summary>
public interface IRunModeSource
{
    /// <summary>Reads the mode and the files.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="SettingsSourceException">The mode cannot be read.</exception>
    Task<RunModeSnapshot> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Writes the values: the mode, then the OmpOS files that changed.</summary>
    /// <param name="values">The values to save.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when everything is written.</returns>
    /// <exception cref="SettingsSourceException">A file cannot be written; it says which and why, and the file is unchanged.</exception>
    Task SaveAsync(RunModeValues values, CancellationToken cancellationToken);
}

namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// Where the Agents page reads and writes its values. A head adapts the terminal settings to this interface; expected failures
/// are reported as <see cref="SettingsSourceException"/>.
/// </summary>
public interface IAgentsSettingsSource
{
    /// <summary>Reads the saved values.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The saved values.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be read.</exception>
    Task<AgentsSettingsValues> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Writes the values.</summary>
    /// <param name="values">The values to save.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the values are written.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be written.</exception>
    Task SaveAsync(AgentsSettingsValues values, CancellationToken cancellationToken);
}

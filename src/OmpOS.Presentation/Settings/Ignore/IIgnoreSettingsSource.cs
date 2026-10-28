namespace OmpOS.Presentation.Settings.Ignore;

/// <summary>Where the Ignore page reads and writes the global list and reads the built-in list and the ignore files of the workspace. An expected failure is a <see cref="SettingsSourceException"/>.</summary>
public interface IIgnoreSettingsSource
{
    /// <summary>Gets the built-in list of ignored files. It is fixed and shown read-only.</summary>
    IReadOnlyList<string> DefaultPatterns { get; }

    /// <summary>Reads the saved values.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The values.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be read.</exception>
    Task<IgnoreSettingsValues> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Writes the values.</summary>
    /// <param name="values">The values to save.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the values are written.</returns>
    /// <exception cref="SettingsSourceException">The values cannot be written.</exception>
    Task SaveAsync(IgnoreSettingsValues values, CancellationToken cancellationToken);

    /// <summary>Lists the ignore files found in the workspace root.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The files that exist, in the order they are ranked.</returns>
    /// <exception cref="SettingsSourceException">The files cannot be read.</exception>
    Task<IReadOnlyList<IgnoreFileRow>> FoundFilesAsync(CancellationToken cancellationToken);
}

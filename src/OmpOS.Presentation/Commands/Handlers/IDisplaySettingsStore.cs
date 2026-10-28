namespace OmpOS.Presentation.Commands.Handlers;

/// <summary>Keeps the display switches between runs, in a file OmpOS owns.</summary>
public interface IDisplaySettingsStore
{
    /// <summary>Reads the saved values.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The saved values, or <see cref="DisplaySettingsValues.Defaults"/> when there is no file or it cannot be read.</returns>
    Task<DisplaySettingsValues> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Saves the values.</summary>
    /// <param name="values">The values.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the values are saved.</returns>
    /// <exception cref="IOException">The values could not be saved.</exception>
    Task SaveAsync(DisplaySettingsValues values, CancellationToken cancellationToken);
}

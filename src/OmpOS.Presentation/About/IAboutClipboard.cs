namespace OmpOS.Presentation.About;

/// <summary>Where the About window puts the text of "Copy version info". A head implements it over its own clipboard; tests use a fake.</summary>
public interface IAboutClipboard
{
    /// <summary>Replaces the clipboard text.</summary>
    /// <param name="text">The text to copy.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the text is on the clipboard.</returns>
    /// <exception cref="InvalidOperationException">The head has no clipboard to write to.</exception>
    Task SetTextAsync(string text, CancellationToken cancellationToken);
}

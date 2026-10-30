namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>Puts text on the system clipboard. Each head implements it with its own clipboard.</summary>
public interface IClipboardWriter
{
    /// <summary>Copies text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns><see langword="true"/> when the clipboard took the text.</returns>
    Task<bool> SetTextAsync(string text, CancellationToken cancellationToken);
}

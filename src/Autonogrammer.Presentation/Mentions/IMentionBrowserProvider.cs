namespace Autonogrammer.Presentation.Mentions;

/// <summary>The built-in browser as a mention source. There is no browser pane in every head, so a head without one passes no provider.</summary>
public interface IMentionBrowserProvider
{
    /// <summary>Gets the title of the open page, or <see langword="null"/> when no page is open.</summary>
    string? CurrentTitle { get; }

    /// <summary>Captures the open page.</summary>
    /// <param name="maxBytes">The byte cap of the text.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>The page.</returns>
    /// <exception cref="MentionUnavailableException">No page is open.</exception>
    Task<MentionBrowserPage> CaptureAsync(int maxBytes, CancellationToken cancellationToken);
}

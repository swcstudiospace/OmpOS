using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Supplies the scripted or live page. Preview never calls it.</summary>
public interface IBrowserPageSource
{
    /// <summary>Gets how many times a page was opened.</summary>
    int Opens { get; }

    /// <summary>Opens the page for a session.</summary>
    /// <param name="sessionId">The session.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The page.</returns>
    ValueTask<ICDPSession> OpenAsync(string sessionId, CancellationToken cancellationToken);
}

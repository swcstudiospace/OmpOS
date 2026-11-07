using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Goes back through Page.navigateToHistoryEntry when history exists.</summary>
public static class BrowserBackTool
{
    /// <summary>The tool name.</summary>
    public const string Name = "browser_back";

    /// <summary>Sends the back command.</summary>
    /// <param name="session">The page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public static async Task BackAsync(ICDPSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await session.SendAsync("Page.goBack", new { }, waitForCallback: true, options: null).ConfigureAwait(false);
    }
}

using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Closes a popup target and reports it. There is no upload tool.</summary>
public static class BrowserCloseTool
{
    /// <summary>The tool name.</summary>
    public const string Name = "browser_close";

    /// <summary>Closes the page.</summary>
    /// <param name="session">The page.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public static async Task CloseAsync(ICDPSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await session.SendAsync("Page.close", new { }, waitForCallback: true, options: null).ConfigureAwait(false);
    }
}

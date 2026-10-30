using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Clicks through Input.dispatchMouseEvent. Coordinates are scaled by the caller.</summary>
public static class BrowserClickTool
{
    /// <summary>The tool name.</summary>
    public const string Name = "browser_click";

    /// <summary>Dispatches a click.</summary>
    /// <param name="session">The page.</param>
    /// <param name="x">The CSS x.</param>
    /// <param name="y">The CSS y.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public static async Task ClickAsync(ICDPSession session, double x, double y, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await session.SendAsync("Input.dispatchMouseEvent", new { type = "mousePressed", x, y, button = "left", clickCount = 1 }, waitForCallback: true, options: null).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await session.SendAsync("Input.dispatchMouseEvent", new { type = "mouseReleased", x, y, button = "left", clickCount = 1 }, waitForCallback: true, options: null).ConfigureAwait(false);
    }
}

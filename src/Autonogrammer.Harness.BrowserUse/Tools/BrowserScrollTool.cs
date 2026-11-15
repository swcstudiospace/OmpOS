using Autonogrammer.Harness.BrowserUse.Chromium;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Scrolls through Input.dispatchMouseEvent.</summary>
public static class BrowserScrollTool
{
    /// <summary>The tool name.</summary>
    public const string Name = "browser_scroll";

    /// <summary>Scrolls by a bounded amount.</summary>
    /// <param name="session">The page.</param>
    /// <param name="deltaY">The delta.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The task.</returns>
    public static async Task ScrollAsync(ICDPSession session, int deltaY, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        cancellationToken.ThrowIfCancellationRequested();
        var json = FormattableString.Invariant($$"""{"type":{{CdpParams.Quoted("mouseWheel")}},"x":0,"y":0,"deltaX":0,"deltaY":{{deltaY}}}""");
        _ = await session.SendAsync("Input.dispatchMouseEvent", CdpParams.Parse(json), waitForCallback: true, options: null).ConfigureAwait(false);
    }
}

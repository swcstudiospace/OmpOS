using System.Text.Json;
using Autonogrammer.Harness.BrowserUse.Chromium;
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
        _ = await session.SendAsync("Input.dispatchMouseEvent", MouseEvent("mousePressed", x, y), waitForCallback: true, options: null).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _ = await session.SendAsync("Input.dispatchMouseEvent", MouseEvent("mouseReleased", x, y), waitForCallback: true, options: null).ConfigureAwait(false);
    }

    private static JsonElement MouseEvent(string type, double x, double y) =>
        CdpParams.Parse(FormattableString.Invariant($$"""{"type":{{CdpParams.Quoted(type)}},"x":{{x}},"y":{{y}},"button":{{CdpParams.Quoted("left")}},"clickCount":1}"""));
}

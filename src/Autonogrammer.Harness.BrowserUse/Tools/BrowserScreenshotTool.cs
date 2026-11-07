using Autonogrammer.Harness.BrowserUse.Vision;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Tools;

/// <summary>Captures one image through Page.captureScreenshot parameters.</summary>
public static class BrowserScreenshotTool
{
    /// <summary>The tool name.</summary>
    public const string Name = "browser_screenshot";

    /// <summary>The failure when the model cannot read images.</summary>
    public const string ModelCannotSeeImages = "ModelCannotSeeImages. Choose a vision model.";

    /// <summary>Sends one capture step. The viewport only changes the clip.</summary>
    /// <param name="session">The page.</param>
    /// <param name="step">The CDP parameters.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The image bytes.</returns>
    public static async Task<byte[]> CaptureAsync(ICDPSession session, ScreenshotStep step, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(step);
        cancellationToken.ThrowIfCancellationRequested();
        var clip = Clip(step.Viewport, step.Scale);
        var payload = await session.SendAsync(
            "Page.captureScreenshot",
            new { format = step.Format, quality = step.Quality, clip, fromSurface = true, captureBeyondViewport = false },
            waitForCallback: true,
            options: null).ConfigureAwait(false);
        if (payload is { } element && element.TryGetProperty("data", out var data))
        {
            return Convert.FromBase64String(data.GetString() ?? string.Empty);
        }

        return [];
    }

    private static object Clip(string viewport, double scale)
    {
        var width = viewport switch
        {
            "tablet" => 768,
            "mobile" => 390,
            _ => ScreenshotPipeline.ViewportWidth,
        };
        return new { x = 0, y = 0, width, height = ScreenshotPipeline.ViewportHeight, scale };
    }
}

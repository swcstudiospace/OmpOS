namespace Autonogrammer.Harness.BrowserUse.Vision;

/// <summary>Keeps one screenshot under the byte cap through CDP parameters. No imaging library.</summary>
public sealed class ScreenshotPipeline
{
    /// <summary>The minimum gap between shots.</summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>The fixed viewport width.</summary>
    public const int ViewportWidth = 1280;

    /// <summary>The fixed viewport height.</summary>
    public const int ViewportHeight = 800;

    private readonly TimeProvider _time;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private DateTimeOffset _last = DateTimeOffset.MinValue;

    /// <summary>Initializes the pipeline.</summary>
    /// <param name="time">The clock.</param>
    /// <param name="delay">The wait used to honour the 500 ms floor. Null waits on the clock token.</param>
    public ScreenshotPipeline(TimeProvider? time = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _time = time ?? TimeProvider.System;
        _delay = delay ?? ((span, token) => Task.Delay(span, _time, token));
    }

    /// <summary>Gets the last enforced wait. Tests read this.</summary>
    public TimeSpan LastWait { get; private set; }

    /// <summary>Captures one image, walking PNG, JPEG 85, JPEG 70, then a smaller clip scale.</summary>
    /// <param name="capture">Sends Page.captureScreenshot and returns the bytes.</param>
    /// <param name="maxBytes">The byte cap.</param>
    /// <param name="viewport">desktop, tablet or mobile. Only the clip changes.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>The capture.</returns>
    public Task<ScreenshotCapture> CaptureAsync(
        Func<ScreenshotStep, CancellationToken, Task<byte[]>> capture,
        int maxBytes,
        string viewport,
        CancellationToken cancellationToken) =>
        RunAsync(capture, maxBytes, viewport, zoom: false, cancellationToken);

    /// <summary>Captures a zoomed region at full resolution, still within the byte cap.</summary>
    /// <param name="capture">The CDP capture.</param>
    /// <param name="maxBytes">The byte cap.</param>
    /// <param name="cancellationToken">Cancels the capture.</param>
    /// <returns>The capture.</returns>
    public Task<ScreenshotCapture> ZoomAsync(
        Func<ScreenshotStep, CancellationToken, Task<byte[]>> capture,
        int maxBytes,
        CancellationToken cancellationToken) =>
        RunAsync(capture, maxBytes, "desktop", zoom: true, cancellationToken);

    private async Task<ScreenshotCapture> RunAsync(
        Func<ScreenshotStep, CancellationToken, Task<byte[]>> capture,
        int maxBytes,
        string viewport,
        bool zoom,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        await HonourIntervalAsync(cancellationToken).ConfigureAwait(false);
        var view = viewport is "tablet" or "mobile" ? viewport : "desktop";
        var steps = zoom
            ? new[] { new ScreenshotStep("png", null, 1, view, true) }
            : new[]
            {
                new ScreenshotStep("png", null, 1, view, false),
                new ScreenshotStep("jpeg", 85, 1, view, false),
                new ScreenshotStep("jpeg", 70, 1, view, false),
                new ScreenshotStep("jpeg", 70, 0.5, view, false),
            };
        var tried = new List<ScreenshotStep>(steps.Length);
        foreach (var step in steps)
        {
            tried.Add(step);
            var bytes = await capture(step, cancellationToken).ConfigureAwait(false);
            if (bytes.Length <= maxBytes)
            {
                var mime = string.Equals(step.Format, "png", StringComparison.Ordinal) ? "image/png" : "image/jpeg";
                return new ScreenshotCapture(bytes, mime, tried, null);
            }
        }

        return new ScreenshotCapture(ReadOnlyMemory<byte>.Empty, "image/png", tried, "ImageBudgetExhausted");
    }

    private async Task HonourIntervalAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var elapsed = now - _last;
        if (_last != DateTimeOffset.MinValue && elapsed < MinimumInterval)
        {
            LastWait = MinimumInterval - elapsed;
            await _delay(LastWait, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            LastWait = TimeSpan.Zero;
        }

        _last = _time.GetUtcNow();
    }
}

using Autonogrammer.Contracts.Cowork.Browser;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Viewer;

/// <summary>
/// The in-process desktop feed. It publishes at most ten frames a second from a JPEG screencast whose long edge is 1280.
/// </summary>
public sealed class LocalBrowserPaneFeed : IBrowserPaneFeed
{
    /// <summary>The frame cap.</summary>
    public const int FramesPerSecond = 10;

    /// <summary>The JPEG quality passed to <c>Page.startScreencast</c>.</summary>
    public const int JpegQuality = 70;

    /// <summary>The long edge passed as <c>maxWidth</c> and <c>maxHeight</c>.</summary>
    public const int LongEdge = 1280;

    private readonly ScreencastPump _pump = new();
    private readonly ICDPSession _session;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _gate = new();
    private DateTimeOffset _lastPublished = DateTimeOffset.MinValue;
    private bool _published;

    /// <summary>Initializes the feed over one page session.</summary>
    /// <param name="session">The page session.</param>
    /// <param name="time">The clock that enforces the frame cap. Null uses <see cref="TimeProvider.System"/>.</param>
    public LocalBrowserPaneFeed(ICDPSession session, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _timeProvider = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public BrowserPaneState State { get; private set; } = BrowserPaneState.Idle;

    /// <inheritdoc />
    public BrowserFrame? LatestFrame { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Attaches the desktop viewer and starts the bounded screencast.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The task.</returns>
    public async Task AttachViewerAsync(CancellationToken cancellationToken = default)
    {
        State = BrowserPaneState.Starting;
        Changed?.Invoke(this, EventArgs.Empty);
        await _pump.AddViewerAsync(_session, ConsumeAsync, JpegQuality, LongEdge, sendLastFrame: true, cancellationToken).ConfigureAwait(false);
        State = BrowserPaneState.Live;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Detaches the viewer. The pump stops the screencast when none remain.</summary>
    /// <param name="cancellationToken">Cancels the stop.</param>
    /// <returns>The task.</returns>
    public async Task DetachViewerAsync(CancellationToken cancellationToken = default)
    {
        await _pump.RemoveViewerAsync(cancellationToken).ConfigureAwait(false);
        State = BrowserPaneState.Idle;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private Task ConsumeAsync(BrowserFrame frame)
    {
        var now = _timeProvider.GetUtcNow();
        var publish = false;
        lock (_gate)
        {
            if (!_published || now - _lastPublished >= TimeSpan.FromSeconds(1d / FramesPerSecond))
            {
                _published = true;
                _lastPublished = now;
                LatestFrame = frame;
                publish = true;
            }
        }

        if (publish)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return Task.CompletedTask;
    }
}

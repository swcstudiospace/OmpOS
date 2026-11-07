using Autonogrammer.Contracts.Cowork.Browser;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Viewer;

/// <summary>
/// Desktop take-over. Pointer and key events become CDP input. Remote viewers are refused. F6 calls <see cref="ReleaseControlAsync"/>.
/// </summary>
public sealed class TakeOverController : IBrowserPaneControl
{
    private readonly ICDPSession _session;
    private readonly bool _localDesktop;
    private BrowserFrame? _frame;

    /// <summary>Initializes a controller over the page that runs on this desktop.</summary>
    /// <param name="session">The page session.</param>
    /// <param name="gate">The call gate. Null creates one.</param>
    /// <param name="localDesktop">False when this head is not the desktop that runs the browser. Input is then refused.</param>
    public TakeOverController(ICDPSession session, BrowserCallGate? gate = null, bool localDesktop = true)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        Gate = gate ?? new BrowserCallGate();
        _localDesktop = localDesktop;
    }

    /// <summary>Gets the gate provider calls wait on.</summary>
    public BrowserCallGate Gate { get; }

    /// <inheritdoc />
    public BrowserControlState Control => Gate.Control;

    /// <inheritdoc />
    public bool KeyboardCaptured { get; private set; }

    /// <summary>Gets the chrome indicator. It is not part of the page frame.</summary>
    public BrowserIndicatorState Indicator => BrowserIndicatorState.For(Control);

    /// <summary>Records the frame whose metadata maps the next pointer event.</summary>
    /// <param name="frame">The consumed frame.</param>
    public void NoteFrame(BrowserFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        _frame = frame;
    }

    /// <summary>Maps a displayed-frame point to viewport CSS pixels using device size and page scale.</summary>
    /// <param name="input">The pointer event.</param>
    /// <param name="metadata">The frame metadata.</param>
    /// <returns>The CSS pixel point. Scroll is not added: CDP mouse events are viewport-relative.</returns>
    public static (double X, double Y) ToCssPixels(BrowserPointerInput input, BrowserFrameMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(metadata);
        var scale = metadata.PageScaleFactor == 0 ? 1 : metadata.PageScaleFactor;
        var width = input.FrameWidth <= 0 ? 1 : input.FrameWidth;
        var height = input.FrameHeight <= 0 ? 1 : input.FrameHeight;
        var x = input.X / width * metadata.DeviceWidth / scale;
        var y = input.Y / height * metadata.DeviceHeight / scale;
        return (x, y);
    }

    /// <inheritdoc />
    public Task TakeOverAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RefuseRemote();
        Gate.EnterUserControl();
        KeyboardCaptured = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseControlAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KeyboardCaptured = false;
        Gate.LeaveUserControl();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendPointerAsync(BrowserPointerInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        RefuseRemote();
        if (_frame is null)
        {
            throw new InvalidOperationException("A screencast frame is required before pointer input.");
        }

        var point = ToCssPixels(input, _frame.Metadata);
        await _session.SendAsync(
            "Input.dispatchMouseEvent",
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["type"] = input.Type,
                ["x"] = point.X,
                ["y"] = point.Y,
                ["button"] = input.Button,
                ["clickCount"] = 1,
            },
            waitForCallback: true,
            options: null).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SendKeyAsync(BrowserKeyInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        RefuseRemote();
        if (input.Text is not null)
        {
            await _session.SendAsync(
                "Input.insertText",
                new Dictionary<string, object>(StringComparer.Ordinal) { ["text"] = input.Text },
                waitForCallback: true,
                options: null).ConfigureAwait(false);
        }

        await _session.SendAsync(
            "Input.dispatchKeyEvent",
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["type"] = input.Type,
                ["key"] = input.Key,
            },
            waitForCallback: true,
            options: null).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        KeyboardCaptured = false;
        Gate.Revoke();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task CloseBrowserAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _session.SendAsync("Browser.close", new Dictionary<string, object>(StringComparer.Ordinal), waitForCallback: true, options: null).ConfigureAwait(false);
    }

    private void RefuseRemote()
    {
        if (!_localDesktop)
        {
            throw new InvalidOperationException("Remote viewers cannot send input.");
        }
    }
}

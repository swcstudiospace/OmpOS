using System.Diagnostics;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse.Chromium;

/// <summary>A Chromium process connected over the host pipe.</summary>
public sealed class LaunchedChromiumSession : IChromiumSession
{
    private readonly Process _process;
    private readonly IBrowser _browser;
    private int _tabs;

    /// <summary>Initializes a session over an already connected browser.</summary>
    /// <param name="process">The host-owned process.</param>
    /// <param name="browser">The pipe-connected browser.</param>
    /// <param name="profileDirectory">The profile directory.</param>
    public LaunchedChromiumSession(Process process, IBrowser browser, string profileDirectory)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentException.ThrowIfNullOrEmpty(profileDirectory);
        _process = process;
        _browser = browser;
        ProfileDirectory = profileDirectory;
    }

    /// <inheritdoc />
    public int ProcessId => _process.Id;

    /// <inheritdoc />
    public string ProfileDirectory { get; }

    /// <inheritdoc />
    public long WorkingSetBytes => _process.WorkingSet64;

    /// <inheritdoc />
    public int TabCount => _tabs;

    /// <inheritdoc />
    public async Task OpenTabAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = await _browser.NewPageAsync().ConfigureAwait(false);
        _tabs++;
    }

    /// <inheritdoc />
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _browser.GetVersionAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CaptureOneFrameAsync(CancellationToken cancellationToken)
    {
        var page = await _browser.NewPageAsync().ConfigureAwait(false);
        var session = await page.CreateCDPSessionAsync().ConfigureAwait(false);
        var frame = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.MessageReceived += (_, args) =>
        {
            if (StringComparer.Ordinal.Equals(args.MessageID, "Page.screencastFrame"))
            {
                frame.TrySetResult(true);
            }
        };
        await session.SendAsync("Page.startScreencast", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["format"] = "jpeg",
            ["maxFramesInFlight"] = 1,
        }).ConfigureAwait(false);
        await frame.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        _tabs++;
    }
    /// <inheritdoc />
    public async Task CloseViaCdpAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _browser.CloseAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void KillProcessTree()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process already left.
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _browser.DisposeAsync().ConfigureAwait(false);
        _process.Dispose();
    }
}

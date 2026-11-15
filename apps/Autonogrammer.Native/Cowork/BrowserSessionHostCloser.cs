using Autonogrammer.Harness.BrowserUse;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Closes a browser host from the composition's synchronous release. The host is <see cref="IAsyncDisposable"/>, so the close starts
/// here and is not awaited: the browsers and their egress proxies are closed in that background task.
/// </summary>
internal sealed class BrowserSessionHostCloser : IDisposable
{
    private readonly BrowserSessionHost _host;
    private int _disposed;

    /// <summary>Initializes the closer.</summary>
    /// <param name="host">The host whose browsers close on release.</param>
    public BrowserSessionHostCloser(BrowserSessionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <summary>Starts closing the host once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _ = CloseAsync(_host);
        }
    }

    private static async Task CloseAsync(BrowserSessionHost host) => await host.DisposeAsync().ConfigureAwait(false);
}

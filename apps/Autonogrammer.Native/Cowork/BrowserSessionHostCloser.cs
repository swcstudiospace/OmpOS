using Autonogrammer.Harness.BrowserUse;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// Closes a browser host when the composition releases. The close starts at most once and its task is held until it is awaited, so no close
/// task is discarded. <see cref="DisposeAsync"/> waits for the close; <see cref="Dispose"/> starts it for a caller that cannot await.
/// </summary>
internal sealed class BrowserSessionHostCloser : IDisposable, IAsyncDisposable
{
    private readonly BrowserSessionHost _host;
    private readonly Lock _gate = new();
    private Task? _closing;

    /// <summary>Initializes the closer.</summary>
    /// <param name="host">The host whose browsers close on release.</param>
    public BrowserSessionHostCloser(BrowserSessionHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <summary>Starts closing the host once. The task is kept so <see cref="DisposeAsync"/> can await it. Prefer <see cref="DisposeAsync"/>.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _closing ??= _host.DisposeAsync().AsTask();
        }
    }

    /// <summary>Closes the host once and waits until its browsers and egress proxies are closed.</summary>
    /// <returns>A task that completes when the host has closed.</returns>
    public async ValueTask DisposeAsync()
    {
        Task closing;
        lock (_gate)
        {
            closing = _closing ??= _host.DisposeAsync().AsTask();
        }

        await closing.ConfigureAwait(false);
    }
}

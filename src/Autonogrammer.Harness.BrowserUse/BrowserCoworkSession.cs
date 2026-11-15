using System.Collections.Concurrent;
using Autonogrammer.Harness.BrowserUse.Chromium;
using PuppeteerSharp;

namespace Autonogrammer.Harness.BrowserUse;

/// <summary>One browser owned by one Cowork session.</summary>
public sealed class BrowserCoworkSession : IAsyncDisposable
{
    private readonly BrowserSessionHost _host;
    private readonly IChromiumSession _session;
    private readonly DateTimeOffset _started;
    private readonly ConcurrentQueue<IAsyncDisposable> _owned = new();
    private DateTimeOffset _lastActivity;
    private bool _closed;

    internal BrowserCoworkSession(BrowserSessionHost host, IChromiumSession session, string profileDirectory, DateTimeOffset started)
    {
        _host = host;
        _session = session;
        ProfileDirectory = profileDirectory;
        _started = started;
        _lastActivity = started;
    }

    /// <summary>Gets the profile directory.</summary>
    public string ProfileDirectory { get; }

    /// <summary>Gets the file mode observed after creation. 0700 on Unix.</summary>
    public UnixFileMode ProfileMode { get; internal set; }

    /// <summary>Gets the open tab count.</summary>
    public int TabCount => _session.TabCount;

    /// <summary>Gets whether CDP close was requested.</summary>
    public bool CdpClosed { get; private set; }

    /// <summary>Gets whether the process tree was killed.</summary>
    public bool ProcessTreeKilled { get; private set; }

    /// <summary>Gets the Chromium process id.</summary>
    public int ProcessId => _session.ProcessId;

    /// <summary>Reads the connected browser version.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The version string.</returns>
    public Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        return _session.GetVersionAsync(cancellationToken);
    }

    /// <summary>Captures one screencast frame.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when one frame arrived.</returns>
    public Task CaptureOneFrameAsync(CancellationToken cancellationToken)
    {
        return _session.CaptureOneFrameAsync(cancellationToken);
    }

    /// <summary>Opens a tab when the tab cap allows it.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the tab is open.</returns>
    public async Task OpenTabAsync(CancellationToken cancellationToken)
    {
        _host.EnsureOpen(this);
        if (_session.TabCount >= _host.Options.MaxTabs)
        {
            throw new BrowserSessionLimitException(BrowserSessionLimit.Tabs);
        }

        await _session.OpenTabAsync(cancellationToken).ConfigureAwait(false);
        _lastActivity = _host.UtcNow();
        await _host.EnforceBoundsAsync(this, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a tab when the tab cap allows it and returns its raw CDP page session.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page session of the new tab.</returns>
    public async Task<ICDPSession> OpenPageAsync(CancellationToken cancellationToken)
    {
        _host.EnsureOpen(this);
        if (_session.TabCount >= _host.Options.MaxTabs)
        {
            throw new BrowserSessionLimitException(BrowserSessionLimit.Tabs);
        }

        var page = await _session.OpenPageAsync(cancellationToken).ConfigureAwait(false);
        _lastActivity = _host.UtcNow();
        await _host.EnforceBoundsAsync(this, cancellationToken).ConfigureAwait(false);
        return page;
    }

    internal void Note(DateTimeOffset activity)
    {
        _lastActivity = activity;
    }

    /// <summary>Ties a resource to this browser. It is disposed when the browser closes, even when the close fails.</summary>
    /// <param name="resource">The resource.</param>
    internal void Own(IAsyncDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (_closed)
        {
            throw new InvalidOperationException("The browser session is closed.");
        }

        _owned.Enqueue(resource);
    }

    internal bool Exceeds(DateTimeOffset now, out BrowserSessionLimit limit)
    {
        if (_session.WorkingSetBytes > (long)_host.Options.MaxBrowserMemoryMiB * 1024 * 1024)
        {
            limit = BrowserSessionLimit.Memory;
            return true;
        }

        if (now - _lastActivity > _host.Options.IdleTimeout)
        {
            limit = BrowserSessionLimit.Idle;
            return true;
        }

        if (now - _started > _host.Options.MaxSessionLifetime)
        {
            limit = BrowserSessionLimit.WallTime;
            return true;
        }

        limit = default;
        return false;
    }

    internal async Task CloseAsync(CancellationToken cancellationToken)
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        try
        {
            await _session.CloseViaCdpAsync(cancellationToken).ConfigureAwait(false);
            CdpClosed = true;
            _session.KillProcessTree();
            ProcessTreeKilled = true;
            await _session.DisposeAsync().ConfigureAwait(false);
            if (Directory.Exists(ProfileDirectory))
            {
                Directory.Delete(ProfileDirectory, recursive: true);
            }
        }
        finally
        {
            while (_owned.TryDequeue(out var resource))
            {
                await resource.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _host.ReleaseAsync(this).ConfigureAwait(false);
    }
}

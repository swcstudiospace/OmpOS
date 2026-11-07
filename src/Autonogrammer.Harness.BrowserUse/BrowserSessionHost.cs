using System.Collections.Concurrent;
using Autonogrammer.Harness.BrowserUse.Chromium;
using Autonogrammer.Harness.BrowserUse.Options;

namespace Autonogrammer.Harness.BrowserUse;

/// <summary>
/// Owns one Chromium process per Cowork session, enforces the lifetime bounds, and wipes the profile on close.
/// Close is CDP, then the process tree, then the directory.
/// </summary>
public sealed class BrowserSessionHost : IAsyncDisposable
{
    private const string AllowedPathChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789._/\\:-";

    private readonly IChromiumLauncher _launcher;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly ConcurrentDictionary<BrowserCoworkSession, byte> _open = new();

    /// <summary>Initializes a host.</summary>
    /// <param name="launcher">The process launcher.</param>
    /// <param name="options">The validated options.</param>
    /// <param name="utcNow">The clock. Null uses <see cref="TimeProvider.System"/>.</param>
    public BrowserSessionHost(IChromiumLauncher launcher, BrowserUseOptions options, Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(options);
        _launcher = launcher;
        Options = options;
        _utcNow = utcNow ?? (() => TimeProvider.System.GetUtcNow());
    }

    /// <summary>Gets the options the bounds are read from.</summary>
    public BrowserUseOptions Options { get; }

    /// <summary>Gets the number of browsers this head still holds.</summary>
    public int OpenCount => _open.Count;

    internal DateTimeOffset UtcNow() => _utcNow();

    /// <summary>Starts one browser for a Cowork session with a fresh 0700 profile.</summary>
    /// <param name="executablePath">The located binary.</param>
    /// <param name="proxyPort">The egress-guard port.</param>
    /// <param name="parentEnvironment">The parent environment to filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The session.</returns>
    public async Task<BrowserCoworkSession> OpenAsync(
        string executablePath,
        int proxyPort,
        IEnumerable<KeyValuePair<string, string?>> parentEnvironment,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(executablePath);
        ArgumentNullException.ThrowIfNull(parentEnvironment);
        if (_open.Count >= Options.MaxConcurrentBrowsers)
        {
            throw new BrowserSessionLimitException(BrowserSessionLimit.ConcurrentBrowsers);
        }

        var profile = Directory.CreateTempSubdirectory("ag-browser-").FullName;
        if (!IsSafePath(profile))
        {
            Directory.Delete(profile, recursive: true);
            throw new BrowserSessionLimitException(BrowserSessionLimit.ProfilePathUnsafe);
        }

        var mode = ApplyOwnerOnly(profile);
        var arguments = ChromiumLaunchPlan.Build(proxyPort, Options.ViewportWidth, Options.ViewportHeight, profile);
        var environment = BrowserEnvironment.Build(parentEnvironment);
        IChromiumSession? session = null;
        try
        {
            session = await _launcher.LaunchAsync(
                new ChromiumLaunchRequest(executablePath, arguments, environment, profile),
                cancellationToken).ConfigureAwait(false);
            var cowork = new BrowserCoworkSession(this, session, profile, _utcNow())
            {
                ProfileMode = mode,
            };
            if (!_open.TryAdd(cowork, 0))
            {
                throw new InvalidOperationException("The session was already tracked.");
            }

            if (_open.Count > Options.MaxConcurrentBrowsers)
            {
                await ReleaseAsync(cowork).ConfigureAwait(false);
                throw new BrowserSessionLimitException(BrowserSessionLimit.ConcurrentBrowsers);
            }

            return cowork;
        }
        catch
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            if (Directory.Exists(profile))
            {
                Directory.Delete(profile, recursive: true);
            }

            throw;
        }
    }

    internal void EnsureOpen(BrowserCoworkSession session)
    {
        if (!_open.ContainsKey(session))
        {
            throw new InvalidOperationException("The browser session is closed.");
        }
    }

    internal async Task EnforceBoundsAsync(BrowserCoworkSession session, CancellationToken cancellationToken)
    {
        if (session.Exceeds(_utcNow(), out var limit))
        {
            await ReleaseAsync(session).ConfigureAwait(false);
            throw new BrowserSessionLimitException(limit);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        _ = cancellationToken;
    }

    /// <summary>Closes every session whose idle, wall or memory bound has elapsed.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number closed.</returns>
    public async Task<int> EnforceBoundsAsync(CancellationToken cancellationToken)
    {
        var closed = 0;
        foreach (var session in _open.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!session.Exceeds(_utcNow(), out _))
            {
                continue;
            }

            await ReleaseAsync(session).ConfigureAwait(false);
            closed++;
        }

        return closed;
    }

    internal async Task ReleaseAsync(BrowserCoworkSession session)
    {
        if (_open.TryRemove(session, out _))
        {
            await session.CloseAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var session in _open.Keys.ToArray())
        {
            await ReleaseAsync(session).ConfigureAwait(false);
        }
    }

    private static bool IsSafePath(string path)
    {
        foreach (var character in path)
        {
            if (!AllowedPathChars.Contains(character, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static UnixFileMode ApplyOwnerOnly(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return UnixFileMode.None;
        }

        const UnixFileMode mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        File.SetUnixFileMode(path, mode);
        return File.GetUnixFileMode(path);
    }
}

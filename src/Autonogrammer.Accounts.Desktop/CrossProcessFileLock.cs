using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts.Desktop;

/// <summary>
/// The desktop <see cref="IRefreshGuard"/>: a lock file opened with <see cref="FileShare.None"/>, so two instances of the app (and two managers of one process)
/// never refresh at once. Once <see cref="AccountSessionManager"/> holds the guard it re-reads the secret store and adopts a refresh token another process
/// stored, so the second holder uses the rotated token instead of spending the consumed one (D-20, AG-67-6).
/// </summary>
/// <remarks>
/// The lock lives in this L3 adapter only; the web-safe <c>Autonogrammer.Accounts</c> library has no file system. The operating system releases the lock when
/// the holder's process ends, so a crash leaves nothing to clean up, and the file itself is never deleted (deleting it would let two holders lock two
/// different files).
/// </remarks>
public sealed class CrossProcessFileLock : IRefreshGuard
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ProcessGates = new(StringComparer.Ordinal);
    private static readonly TimeSpan FirstPoll = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan LongestPoll = TimeSpan.FromMilliseconds(250);

    private readonly string _path;
    private readonly SemaphoreSlim _processGate;
    private readonly TimeProvider _time;
    private readonly ILogger<CrossProcessFileLock>? _logger;

    /// <summary>Initializes a new instance of the <see cref="CrossProcessFileLock"/> class.</summary>
    /// <param name="lockFilePath">The lock file, in the per-user state directory. Its directory is created when missing; the file is created owner-only.</param>
    /// <param name="timeProvider">The clock of the wait between attempts and of <see cref="MaximumWait"/>; null means the system clock.</param>
    /// <param name="logger">An optional logger. It never receives the path.</param>
    public CrossProcessFileLock(string lockFilePath, TimeProvider? timeProvider = null, ILogger<CrossProcessFileLock>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);
        _path = Path.GetFullPath(lockFilePath);
        _processGate = ProcessGates.GetOrAdd(_path, static _ => new SemaphoreSlim(1, 1));
        _time = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    /// <summary>Gets how long a holder may wait for the lock before <see cref="AcquireAsync"/> throws a <see cref="TimeoutException"/>. A refresh holds the lock for at most its 15 s request.</summary>
    public static TimeSpan MaximumWait { get; } = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        long started = _time.GetTimestamp();
        if (!await _processGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            DesktopLogMessages.RefreshLockWaiting(_logger);
            await _processGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        bool handedOver = false;
        try
        {
            IAsyncDisposable handle = await OpenExclusiveAsync(started, cancellationToken).ConfigureAwait(false);
            handedOver = true;
            DesktopLogMessages.RefreshLockAcquired(_logger);
            return handle;
        }
        finally
        {
            if (!handedOver)
            {
                _ = _processGate.Release();
            }
        }
    }

    /// <summary>Names the type. The path is not included.</summary>
    /// <returns>The type name.</returns>
    public override string ToString() => nameof(CrossProcessFileLock);

    private static bool IsContention(IOException exception) =>
        exception is not (DirectoryNotFoundException or FileNotFoundException or PathTooLongException or DriveNotFoundException);

    private async Task<Handle> OpenExclusiveAsync(long started, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        var options = new FileStreamOptions
        {
            Mode = FileMode.OpenOrCreate,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 1,
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        TimeSpan poll = FirstPoll;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Handle? opened = TryOpen(options);
            if (opened is not null)
            {
                return opened;
            }

            if (_time.GetElapsedTime(started) > MaximumWait)
            {
                DesktopLogMessages.RefreshLockTimedOut(_logger);
                throw new TimeoutException("The refresh lock stayed held too long.");
            }

            await Task.Delay(poll, _time, cancellationToken).ConfigureAwait(false);
            poll = poll * 2 > LongestPoll ? LongestPoll : poll * 2;
        }
    }

    private Handle? TryOpen(FileStreamOptions options)
    {
        FileStream? stream = null;
        try
        {
            stream = new FileStream(_path, options);
            var handle = new Handle(stream, _processGate, _logger);
            stream = null;
            return handle;
        }
        catch (IOException exception) when (IsContention(exception))
        {
            return null;
        }
        finally
        {
            stream?.Dispose();
        }
    }

    private sealed class Handle(FileStream stream, SemaphoreSlim processGate, ILogger? logger) : IAsyncDisposable
    {
        private int _released;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0)
            {
                return;
            }

            try
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                _ = processGate.Release();
                DesktopLogMessages.RefreshLockReleased(logger);
            }
        }
    }
}

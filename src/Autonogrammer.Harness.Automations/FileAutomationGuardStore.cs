using System.Globalization;
using System.Text.Json;

namespace Autonogrammer.Harness.Automations;

/// <summary>
/// Keeps automation pauses and the history of admitted runs in one versioned JSON file, so both survive a restart.
/// Every call reads the file afresh. The store fails closed: a file that is unreadable, empty, corrupt, or written with an
/// unknown version makes every call throw <see cref="InvalidDataException"/> or the underlying I/O exception, and the
/// engine refuses the run it was consulted for instead of admitting it. The file is never overwritten or quarantined in
/// that state, so an operator can inspect it. A file that does not exist is an empty history. Writes go to a temporary
/// file in the same directory that only the owner can access, and replace the state by rename, so a crash leaves either
/// the old file or the new one. The file holds automation identifiers, guard names, reasons and instants, no secrets.
/// </summary>
public sealed class FileAutomationGuardStore : IAutomationGuardStore, IDisposable
{
    /// <summary>The only document version this store reads and writes.</summary>
    internal const int CurrentVersion = 1;

    private const UnixFileMode OwnerDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileAutomationGuardStore"/> class.
    /// </summary>
    /// <param name="path">The guard store file. Its directory is created on the first write.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty or white space.</exception>
    public FileAutomationGuardStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <summary>
    /// Releases the lock that serializes access. The state on disk is already current after every change.
    /// </summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc />
    public async ValueTask<AutomationPause?> FindPauseAsync(string automationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return document.Pauses!.TryGetValue(automationId, out var stored) ? ToPause(stored) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask PauseAsync(string automationId, AutomationPause pause, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        ArgumentNullException.ThrowIfNull(pause);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            document.Pauses![automationId] = new GuardStorePause
            {
                Guard = pause.Guard,
                Reason = pause.Reason,
                HowToResume = pause.HowToResume,
            };
            await WriteAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask ResumeAsync(string automationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (document.Pauses!.Remove(automationId))
            {
                await WriteAsync(document, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> CountRunsSinceAsync(string automationId, DateTimeOffset since, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return document.Runs!.TryGetValue(automationId, out var starts) ? starts.Count(start => start > since) : 0;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask RecordRunAsync(string automationId, DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(automationId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var runs = document.Runs!;
            if (!runs.TryGetValue(automationId, out var starts))
            {
                starts = [];
                runs[automationId] = starts;
            }

            starts.Add(startedAt);
            await WriteAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static AutomationPause ToPause(GuardStorePause stored)
    {
        try
        {
            return new AutomationPause(stored.Guard ?? string.Empty, stored.Reason ?? string.Empty, stored.HowToResume ?? string.Empty);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("A pause in the automation guard store has a blank field.", exception);
        }
    }

    // Reads the document. A missing file is an empty history; anything else that is not a valid current-version document
    // throws, so the caller never treats damage as "no pauses and no runs".
    private async ValueTask<GuardStoreDocument> LoadAsync(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return Empty();
        }
        catch (DirectoryNotFoundException)
        {
            return Empty();
        }

        GuardStoreDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, FileAutomationGuardStoreJsonContext.Default.GuardStoreDocument);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The automation guard store '{_path}' is corrupt and was not read.", exception);
        }

        if (document is null || document.Pauses is null || document.Runs is null)
        {
            throw new InvalidDataException($"The automation guard store '{_path}' is empty or incomplete and was not read.");
        }

        if (document.Version != CurrentVersion)
        {
            throw new InvalidDataException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The automation guard store '{_path}' has version {document.Version}, but only version {CurrentVersion} is supported."));
        }

        foreach (var (automationId, stored) in document.Pauses)
        {
            if (stored is null)
            {
                throw new InvalidDataException($"The automation guard store '{_path}' has an empty pause for '{automationId}' and was not read.");
            }

            _ = ToPause(stored);
        }

        if (document.Runs.Values.Any(starts => starts is null))
        {
            throw new InvalidDataException($"The automation guard store '{_path}' has an empty run list and was not read.");
        }

        return document;
    }

    private static GuardStoreDocument Empty() =>
        new()
        {
            Version = CurrentVersion,
            Pauses = new Dictionary<string, GuardStorePause>(StringComparer.Ordinal),
            Runs = new Dictionary<string, List<DateTimeOffset>>(StringComparer.Ordinal),
        };

    private async ValueTask WriteAsync(GuardStoreDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(directory);
            }
            else
            {
                Directory.CreateDirectory(directory, OwnerDirectoryMode);
            }
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, FileAutomationGuardStoreJsonContext.Default.GuardStoreDocument);
        var temporary = $"{_path}.{Guid.NewGuid().ToString("N")}.tmp";
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerFileMode;
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                RandomAccess.FlushToDisk(stream.SafeFileHandle);
            }

            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}

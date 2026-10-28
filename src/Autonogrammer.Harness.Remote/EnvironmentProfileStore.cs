using System.Globalization;
using System.Text.Json;
using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// Keeps the user's remote environment profiles in one versioned JSON file under the per-user application data directory. The file holds
/// references only (host aliases, identity file paths, directories): an <see cref="EnvironmentProfile"/> has no property that could carry a password,
/// passphrase, key or token, and every profile is validated before it is written and when it is read. Every call reads the file afresh. A file that is
/// corrupt, empty, or of an unknown version reads as no profiles and is moved aside (quarantined); reading never throws for a damaged file. Writes go to
/// a temporary file in the same directory (readable by the owner only) that replaces the file by rename, so a reader sees the old or the new document.
/// </summary>
public sealed class EnvironmentProfileStore : IDisposable
{
    /// <summary>The most profiles the store keeps.</summary>
    public const int MaxProfiles = 64;

    private const int CurrentVersion = 1;

    private readonly string _path;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="EnvironmentProfileStore"/> class.</summary>
    /// <param name="path">The file. Its directory is created on the first write.</param>
    /// <param name="clock">Stamps the last-used time. The system clock when null.</param>
    public EnvironmentProfileStore(string path, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Returns the file in the user's configuration directory: <c>Autonogrammer/environments.json</c> under the per-user application data folder.</summary>
    /// <returns>The absolute path.</returns>
    /// <exception cref="InvalidOperationException">The platform has no per-user configuration directory.</exception>
    public static string DefaultPath()
    {
        var configuration = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrEmpty(configuration))
        {
            throw new InvalidOperationException("The platform has no per-user configuration directory for the environment profile file.");
        }

        return Path.Combine(configuration, "Autonogrammer", "environments.json");
    }

    /// <summary>Releases the lock that serializes writes.</summary>
    public void Dispose() => _gate.Dispose();

    /// <summary>Lists the stored profiles.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The valid profiles with their last-used time, and a warning when something could not be read.</returns>
    public async Task<EnvironmentProfileListing> ListAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (document, damaged) = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var valid = new List<StoredEnvironmentProfile>(document.Profiles.Count);
            var ignored = 0;
            foreach (var profile in document.Profiles)
            {
                if (EnvironmentProfileValidator.IsValid(profile))
                {
                    DateTimeOffset? used = document.LastUsed.TryGetValue(profile.Id, out var when) ? when : null;
                    valid.Add(new StoredEnvironmentProfile(profile, used));
                }
                else
                {
                    ignored++;
                }
            }

            return new EnvironmentProfileListing(valid, Warn(damaged, ignored));
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Adds a profile, or replaces the profile with the same identifier (its position and last-used time stay).</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is replaced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException">The profile is not valid (<see cref="EnvironmentProfileValidator"/>), or the store already holds <see cref="MaxProfiles"/> other profiles.</exception>
    public async Task SaveAsync(EnvironmentProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var issues = EnvironmentProfileValidator.Validate(profile);
        if (issues.Count > 0)
        {
            throw new ArgumentException("The profile is not valid: " + string.Join(" ", issues.Select(issue => issue.Property + ": " + issue.Message)), nameof(profile));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (document, _) = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var index = document.Profiles.FindIndex(existing => string.Equals(existing.Id, profile.Id, StringComparison.Ordinal));
            if (index >= 0)
            {
                document.Profiles[index] = profile;
            }
            else if (document.Profiles.Count >= MaxProfiles)
            {
                throw new ArgumentException(string.Create(CultureInfo.InvariantCulture, $"At most {MaxProfiles} environments can be stored."), nameof(profile));
            }
            else
            {
                document.Profiles.Add(profile);
            }

            await WriteAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Deletes a profile and its last-used time.</summary>
    /// <param name="id">The profile identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when a profile was deleted.</returns>
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (document, _) = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Profiles.RemoveAll(existing => string.Equals(existing.Id, id, StringComparison.Ordinal));
            if (removed == 0)
            {
                return false;
            }

            _ = document.LastUsed.Remove(id);
            await WriteAsync(document, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Records that a session started on a profile now.</summary>
    /// <param name="id">The profile identifier.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns><see langword="true"/> when the profile exists and its last-used time was updated.</returns>
    public async Task<bool> RecordUsedAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (document, _) = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!document.Profiles.Exists(existing => string.Equals(existing.Id, id, StringComparison.Ordinal)))
            {
                return false;
            }

            document.LastUsed[id] = _clock.GetUtcNow();
            await WriteAsync(document, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private static string? Warn(bool damaged, int ignored)
    {
        if (damaged)
        {
            return "The environments file could not be read and was set aside; it is shown as empty. Add the environments again.";
        }

        return ignored == 0
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{ignored} stored environment(s) were ignored because they are not valid.");
    }

    private async Task<(EnvironmentStoreDocument Document, bool Damaged)> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return (new EnvironmentStoreDocument { Version = CurrentVersion }, false);
        }

        EnvironmentStoreDocument? document = null;
        try
        {
            var bytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
            document = JsonSerializer.Deserialize(bytes, EnvironmentStoreJsonContext.Default.EnvironmentStoreDocument);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _ = exception;
        }

        if (document is { Version: CurrentVersion })
        {
            return (document, false);
        }

        Quarantine();
        return (new EnvironmentStoreDocument { Version = CurrentVersion }, true);
    }

    private void Quarantine()
    {
        var aside = $"{_path}.damaged-{_clock.GetUtcNow().ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}";
        try
        {
            File.Move(_path, aside, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A file that cannot be moved aside is replaced by the next write.
            _ = exception;
        }
    }

    private async Task WriteAsync(EnvironmentStoreDocument document, CancellationToken cancellationToken)
    {
        document.Version = CurrentVersion;
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            if (OperatingSystem.IsWindows())
            {
                _ = Directory.CreateDirectory(directory);
            }
            else
            {
                _ = Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, EnvironmentStoreJsonContext.Default.EnvironmentStoreDocument);
        var temporary = $"{_path}.{Guid.NewGuid().ToString("N")}.tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None, Options = FileOptions.Asynchronous };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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

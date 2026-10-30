using System.Globalization;
using System.Text.Json;

namespace Autonogrammer.Workspace;

/// <summary>
/// Keeps setup consent in one versioned JSON file under the user's configuration directory. Every call reads the file
/// afresh, so an approval written by another Autonogrammer process is seen at once. A file that is corrupt, empty, or written
/// with an unknown version is treated as no consent and moved aside (quarantined) so the next approval starts a clean
/// file; reading never throws for a damaged file. Writes go to a temporary file in the same directory and replace the
/// state by rename, so a crash leaves either the old file or the new one.
/// </summary>
public sealed class FileSetupConsentStore : ISetupConsentStore, IDisposable
{
    /// <summary>The only document version this store reads and writes.</summary>
    internal const int CurrentVersion = 1;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _path;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSetupConsentStore"/> class.
    /// </summary>
    /// <param name="path">The consent file. Its directory is created on the first write.</param>
    /// <param name="clock">Stamps approvals. The system clock when null.</param>
    public FileSetupConsentStore(string path, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Returns the consent file in the user's configuration directory: <c>Autonogrammer/setup-consent.json</c> under the
    /// per-user application data folder (<c>$XDG_CONFIG_HOME</c> or <c>~/.config</c> on Linux, <c>%APPDATA%</c> on Windows).
    /// </summary>
    /// <exception cref="InvalidOperationException">The platform has no per-user configuration directory.</exception>
    public static string DefaultPath()
    {
        var configuration = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrEmpty(configuration))
        {
            throw new InvalidOperationException("The platform has no per-user configuration directory for the setup consent file.");
        }

        return Path.Combine(configuration, "Autonogrammer", "setup-consent.json");
    }

    /// <summary>
    /// Releases the lock that serializes writes. The state on disk is already current after every change.
    /// </summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc />
    public async Task<bool> IsGrantedAsync(SetupConsentKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return document.Grants.Exists(grant => Matches(grant, key));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RecordGrantAsync(SetupConsentKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            document.Grants.RemoveAll(grant => string.Equals(grant.Repository, key.RepositoryPath, PathComparison));
            document.Grants.Add(new SetupConsentGrantDocument
            {
                Repository = key.RepositoryPath,
                SetupSha256 = key.SetupSha256,
                GrantedAt = _clock.GetUtcNow(),
            });
            await WriteAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool Matches(SetupConsentGrantDocument grant, SetupConsentKey key) =>
        string.Equals(grant.Repository, key.RepositoryPath, PathComparison)
        && string.Equals(grant.SetupSha256, key.SetupSha256, StringComparison.OrdinalIgnoreCase);

    private async Task<SetupConsentDocument> LoadAsync(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return NewDocument();
        }
        catch (UnauthorizedAccessException)
        {
            return NewDocument();
        }

        SetupConsentDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, SetupConsentJsonContext.Default.SetupConsentDocument);
        }
        catch (JsonException)
        {
            document = null;
        }

        if (document is null || document.Version != CurrentVersion || document.Grants is null)
        {
            Quarantine();
            return NewDocument();
        }

        return document;
    }

    private static SetupConsentDocument NewDocument() => new() { Version = CurrentVersion };

    /// <summary>
    /// Moves an unreadable file aside so it is kept for inspection and the next write starts a clean file. The move is
    /// best effort: if it fails the damaged file is replaced by the next approval.
    /// </summary>
    private void Quarantine()
    {
        var stamp = _clock.GetUtcNow().ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var target = $"{_path}.quarantined-{stamp}-{Guid.NewGuid().ToString("N")}";
        try
        {
            File.Move(_path, target);
        }
        catch (IOException)
        {
            // The damaged file stays where it is; it still grants nothing, and the next approval replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: the file is not trusted whether or not it could be moved.
        }
    }

    private async Task WriteAsync(SetupConsentDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, SetupConsentJsonContext.Default.SetupConsentDocument);
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

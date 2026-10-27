using System.Globalization;
using System.Text.Json;

namespace OmpOS.Workspace.Sets;

/// <summary>
/// Keeps the user's recent workspace sets in one versioned JSON file under the per-user configuration directory, newest first,
/// at most <see cref="MaxRecent"/> of them. Every call reads the file afresh. A file that is corrupt, empty, or of an unknown
/// version reads as no sets and is moved aside (quarantined); reading never throws for a damaged file. Writes go to a temporary file in the same
/// directory and replace the state by rename.
/// </summary>
public sealed class WorkspaceSetStore : IDisposable
{
    /// <summary>The most recent sets kept. Recording one more drops the oldest.</summary>
    public const int MaxRecent = 20;

    /// <summary>The only document version this store reads and writes.</summary>
    internal const int CurrentVersion = 1;

    private readonly string _path;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceSetStore"/> class.
    /// </summary>
    /// <param name="path">The file. Its directory is created on the first write.</param>
    /// <param name="clock">Stamps recorded sets. The system clock when null.</param>
    public WorkspaceSetStore(string path, TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Returns the file in the user's configuration directory: <c>OmpOS/workspace-sets.json</c> under the per-user application data folder.
    /// </summary>
    /// <exception cref="InvalidOperationException">The platform has no per-user configuration directory.</exception>
    public static string DefaultPath()
    {
        var configuration = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrEmpty(configuration))
        {
            throw new InvalidOperationException("The platform has no per-user configuration directory for the workspace set file.");
        }

        return Path.Combine(configuration, "OmpOS", "workspace-sets.json");
    }

    /// <summary>
    /// Releases the lock that serializes writes.
    /// </summary>
    public void Dispose() => _gate.Dispose();

    /// <summary>
    /// Lists the remembered sets.
    /// </summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sets, most recently used first. An entry that no longer forms a set is left out.</returns>
    public async Task<IReadOnlyList<RecentWorkspaceSet>> ListAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var sets = new List<RecentWorkspaceSet>(document.Sets.Count);
            foreach (var entry in document.Sets)
            {
                if (TryRestore(entry) is { } restored)
                {
                    sets.Add(restored);
                }
            }

            return sets;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Remembers a set as the most recently used. A set with the same identifier is replaced.
    /// </summary>
    /// <param name="set">The set to remember.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task RecordAsync(WorkspaceSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            document.Sets.RemoveAll(entry => string.Equals(entry.Id, set.Id, StringComparison.Ordinal));
            document.Sets.Insert(0, ToEntry(set, _clock.GetUtcNow()));
            if (document.Sets.Count > MaxRecent)
            {
                document.Sets.RemoveRange(MaxRecent, document.Sets.Count - MaxRecent);
            }

            await WriteAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Forgets a set.
    /// </summary>
    /// <param name="setId">The identifier of the set.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when a set was removed.</returns>
    public async Task<bool> ForgetAsync(string setId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(setId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            var removed = document.Sets.RemoveAll(entry => string.Equals(entry.Id, setId, StringComparison.Ordinal)) > 0;
            if (removed)
            {
                await WriteAsync(document, cancellationToken).ConfigureAwait(false);
            }

            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static WorkspaceSetEntryDocument ToEntry(WorkspaceSet set, DateTimeOffset now) => new()
    {
        Id = set.Id,
        Name = set.Name,
        LastUsedAt = now,
        Roots = set.Roots
            .Select(root => new WorkspaceSetRootDocument
            {
                Id = root.Id,
                Path = root.Path,
                DisplayName = root.DisplayName,
                EnvironmentId = root.EnvironmentId,
            })
            .ToList(),
    };

    private static RecentWorkspaceSet? TryRestore(WorkspaceSetEntryDocument entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.Name) || entry.Roots is not { Count: > 0 })
        {
            return null;
        }

        var roots = new List<WorkspaceSetRoot>(entry.Roots.Count);
        foreach (var root in entry.Roots)
        {
            if (root is null || string.IsNullOrWhiteSpace(root.Path))
            {
                return null;
            }

            roots.Add(new WorkspaceSetRoot(root.Id, root.Path, root.DisplayName, root.EnvironmentId, IsPrimary: roots.Count == 0));
        }

        return new RecentWorkspaceSet(new WorkspaceSet(entry.Id, entry.Name, roots), entry.LastUsedAt);
    }

    private static WorkspaceSetDocument NewDocument() => new() { Version = CurrentVersion };

    private async Task<WorkspaceSetDocument> LoadAsync(CancellationToken cancellationToken)
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

        WorkspaceSetDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, WorkspaceSetJsonContext.Default.WorkspaceSetDocument);
        }
        catch (JsonException)
        {
            document = null;
        }

        if (document is null || document.Version != CurrentVersion || document.Sets is null)
        {
            Quarantine();
            return NewDocument();
        }

        return document;
    }

    /// <summary>
    /// Moves an unreadable file aside so it is kept for inspection and the next write starts a clean file. Best effort.
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
            // The damaged file stays where it is; it lists nothing, and the next recorded set replaces it.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: the file is not trusted whether or not it could be moved.
        }
    }

    private async Task WriteAsync(WorkspaceSetDocument document, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, WorkspaceSetJsonContext.Default.WorkspaceSetDocument);
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

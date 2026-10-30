using System.Collections.Immutable;
using System.Text.Json;

namespace Autonogrammer.Harness.Herdr;

/// <summary>
/// The local record of the herdr sessions this host manages. Each change is written to a temporary file in the same
/// directory, flushed to disk, and then moved over the store file. A failed write leaves the previous document in force.
/// A store file that cannot be read or parsed makes <see cref="OpenAsync"/> throw. The store is never reset silently.
/// </summary>
public sealed class HerdrSessionStore : IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private ImmutableDictionary<string, HerdrStoredSession> _sessions;

    private HerdrSessionStore(string path, ImmutableDictionary<string, HerdrStoredSession> sessions)
    {
        _path = path;
        _sessions = sessions;
    }

    /// <summary>
    /// Opens the store at <paramref name="path"/>, or starts empty when no file exists there. A path that exists but cannot be
    /// read makes the open fail; it is never treated as an empty store.
    /// </summary>
    /// <param name="path">The store file. Its directory is created on the first write.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The opened store.</returns>
    /// <exception cref="JsonException">
    /// The file exists but is not a valid store of this version. The store refuses to open, so no session is assumed.
    /// </exception>
    public static async Task<HerdrSessionStore> OpenAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var sessions = ImmutableDictionary.Create<string, HerdrStoredSession>(StringComparer.Ordinal);
        byte[]? bytes = null;
        try
        {
            bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            // No store file yet: the store starts empty, and its directory is created on the first write.
        }
        catch (DirectoryNotFoundException)
        {
            // The store's directory does not exist yet, so there is no store file: the store starts empty.
        }

        if (bytes is not null)
        {
            var document = JsonSerializer.Deserialize(bytes, HerdrJsonContext.Default.HerdrSessionDocument)
                ?? throw new JsonException("The herdr session store is empty.");
            sessions = FromDocument(document);
        }

        return new HerdrSessionStore(fullPath, sessions);
    }

    /// <summary>Releases the lock that serializes writes. The file on disk is already current after every change.</summary>
    public void Dispose() => _writeGate.Dispose();

    /// <summary>Gets every stored session, ordered by identifier.</summary>
    public IReadOnlyList<HerdrStoredSession> Sessions =>
        [.. _sessions.Values.OrderBy(session => session.Id.Value, StringComparer.Ordinal)];

    /// <summary>Returns the stored record for a session, or null when the store has none.</summary>
    /// <param name="id">The session identifier.</param>
    public HerdrStoredSession? Find(HerdrSessionId id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _sessions.TryGetValue(id.Value, out var session) ? session : null;
    }

    /// <summary>Inserts or replaces the record for a session and persists the whole store before returning.</summary>
    /// <param name="session">The record to store.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <exception cref="IOException">The file could not be written or replaced. The previous contents stay in force.</exception>
    public async Task UpsertAsync(HerdrStoredSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var next = _sessions.SetItem(session.Id.Value, session);
            await WriteAsync(next, cancellationToken).ConfigureAwait(false);
            _sessions = next;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    /// <summary>
    /// Applies <paramref name="change"/> to the current record of a session and persists the result before returning. The
    /// current record is read under the write lock, so a change another writer made is never overwritten by a copy taken
    /// earlier. A change that returns the record unchanged writes nothing.
    /// </summary>
    /// <param name="id">The session whose record changes. The change must keep this identifier.</param>
    /// <param name="change">Computes the new record from the current one.</param>
    /// <param name="cancellationToken">Cancels the wait for the write lock and the write.</param>
    /// <returns>The record after the change.</returns>
    /// <exception cref="HerdrSessionNotFoundException">The store has no record for the session.</exception>
    /// <exception cref="ArgumentException">The change returned a record for a different session.</exception>
    /// <exception cref="IOException">The file could not be written or replaced. The previous contents stay in force.</exception>
    public async Task<HerdrStoredSession> UpdateAsync(
        HerdrSessionId id,
        Func<HerdrStoredSession, HerdrStoredSession> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(change);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = Find(id) ?? throw new HerdrSessionNotFoundException($"The local store has no herdr session {id}.");
            var updated = change(current);
            if (updated.Id != id)
            {
                throw new ArgumentException("A change to a stored session must keep its identifier.", nameof(change));
            }

            if (updated == current)
            {
                return current;
            }

            var next = _sessions.SetItem(id.Value, updated);
            await WriteAsync(next, cancellationToken).ConfigureAwait(false);
            _sessions = next;
            return updated;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private static ImmutableDictionary<string, HerdrStoredSession> FromDocument(HerdrSessionDocument document)
    {
        if (document.Version != HerdrSessionDocument.CurrentVersion)
        {
            throw new JsonException("The herdr session store has an unsupported version.");
        }

        var entries = document.Sessions ?? throw new JsonException("The herdr session store has no session list.");
        var sessions = ImmutableDictionary.CreateBuilder<string, HerdrStoredSession>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw new JsonException("The herdr session store has a null session entry.");
            }

            var session = ToStoredSession(entry);
            if (!sessions.TryAdd(session.Id.Value, session))
            {
                throw new JsonException("The herdr session store lists one session more than once.");
            }
        }

        return sessions.ToImmutable();
    }

    private static HerdrStoredSession ToStoredSession(HerdrStoredSessionDocument entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ProviderId))
        {
            throw new JsonException("The herdr session store has a session with no provider.");
        }

        if (entry.LastSequence < 0)
        {
            throw new JsonException("The herdr session store has a session with a negative sequence.");
        }

        if (!Enum.TryParse<HerdrSessionState>(entry.LastKnownState, ignoreCase: false, out var state) || !Enum.IsDefined(state))
        {
            throw new JsonException("The herdr session store has a session with an unknown state.");
        }

        try
        {
            return new HerdrStoredSession(
                new HerdrSessionId(entry.SessionId),
                entry.ProviderId,
                entry.NativeReference is null ? null : new NativeSessionReference(entry.NativeReference),
                state,
                entry.LastSequence);
        }
        catch (ArgumentException failure)
        {
            throw new JsonException("The herdr session store has a session with an invalid identifier or reference.", failure);
        }
    }

    private static HerdrSessionDocument ToDocument(ImmutableDictionary<string, HerdrStoredSession> sessions) => new()
    {
        Version = HerdrSessionDocument.CurrentVersion,
        Sessions =
        [
            .. sessions.Values
                .OrderBy(session => session.Id.Value, StringComparer.Ordinal)
                .Select(session => new HerdrStoredSessionDocument
                {
                    SessionId = session.Id.Value,
                    ProviderId = session.ProviderId,
                    NativeReference = session.NativeReference?.Value,
                    LastKnownState = $"{session.LastKnownState}",
                    LastSequence = session.LastSequence,
                }),
        ],
    };

    private async Task WriteAsync(ImmutableDictionary<string, HerdrStoredSession> sessions, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            // Only the leaf directory created by this call is restricted to its owner. Missing ancestors keep the default
            // mode, and an existing directory keeps its mode. The leaf is checked for a symbolic link just before its mode
            // changes. That check narrows a race with another process but does not close it.
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows())
            {
                if (new DirectoryInfo(directory).LinkTarget is not null)
                {
                    throw new IOException("The herdr session store directory is a symbolic link, so its mode is not changed.");
                }

                File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(ToDocument(sessions), HerdrJsonContext.Default.HerdrSessionDocument);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var replaced = false;
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows())
            {
                // The store holds session references, so only its owner may read or write it.
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporary, options))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                // .NET has no async flush to disk, so the fsync runs on the thread pool; it must not be cancelled mid-flush.
                await Task.Run(() => stream.Flush(flushToDisk: true), CancellationToken.None).ConfigureAwait(false);
            }

            // The rename is not followed by a flush of the directory entry: .NET has no portable API that flushes a
            // directory, so a crash just after the rename can leave the previous document in force.
            File.Move(temporary, _path, overwrite: true);
            replaced = true;
        }
        finally
        {
            if (!replaced)
            {
                DeleteTemporary(temporary);
            }
        }
    }

    // Runs only while a write is failing, so the exception already in flight is the one reported. A temporary file that
    // cannot be deleted is left behind; the store reads only its own path, so the leftover file is never used.
    private static void DeleteTemporary(string temporary)
    {
        try
        {
            File.Delete(temporary);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

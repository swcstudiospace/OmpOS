using System.Text.Json;

namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// Keeps automations, their run history and their schedule cursors in one versioned JSON file, so they survive a restart. Every call
/// reads the file afresh and a change replaces it atomically (see <see cref="AtomicFile"/>). The store fails closed: a file that is
/// unreadable, empty, corrupt or written with an unknown version makes every call throw <see cref="InvalidDataException"/> or the
/// underlying I/O exception, and the file is never overwritten in that state, so an operator can inspect it. A missing file is an empty
/// store. The file holds definitions, secret NAMES and history: no webhook secret and no secret value.
/// </summary>
public sealed class FileAutomationStore : IAutomationStore, IDisposable
{
    /// <summary>The only document version this store reads and writes.</summary>
    internal const int CurrentVersion = 1;

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="FileAutomationStore"/> class.</summary>
    /// <param name="path">The store file. Its directory is created on the first write.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty or white space.</exception>
    public FileAutomationStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <summary>Releases the lock that serializes access. The state on disk is already current after every change.</summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UserAutomation>> ListAsync(CancellationToken cancellationToken) =>
        await ReadAsync(static document => document.List(), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async ValueTask<UserAutomation?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return await ReadAsync(document => document.Get(id), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(UserAutomation automation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(automation);
        _ = await ChangeAsync(
            document =>
            {
                document.Save(automation);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return await ChangeAsync(document => document.Remove(id), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<DateTimeOffset?> GetScheduleCursorAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return await ReadAsync(document => document.GetCursor(id), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetScheduleCursorAsync(string id, DateTimeOffset cursor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        _ = await ChangeAsync(document => document.SetCursor(id, cursor), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<AutomationRunEntry>> GetHistoryAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return await ReadAsync(document => document.GetHistory(id), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask UpsertRunAsync(string id, AutomationRunEntry entry, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(entry);
        _ = await ChangeAsync(document => document.UpsertRun(id, entry), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<T> ReadAsync<T>(Func<AutomationStoreDocument, T> read, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return read(document);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async ValueTask<bool> ChangeAsync(Func<AutomationStoreDocument, bool> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!change(document))
            {
                return false;
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(document, FileAutomationStoreJsonContext.Default.AutomationStoreDocument);
            await AtomicFile.WriteAsync(_path, bytes, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async ValueTask<AutomationStoreDocument> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return AutomationStoreDocument.Empty();
        }

        var bytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        AutomationStoreDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, FileAutomationStoreJsonContext.Default.AutomationStoreDocument);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The automation store '{_path}' is not valid and was not read.", exception);
        }

        if (document is null)
        {
            throw new InvalidDataException($"The automation store '{_path}' is empty and was not read.");
        }

        document.EnsureValid(_path);
        return document;
    }
}

using System.Text.Json;
using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Cowork.Tools;

/// <summary>Persists grants as owner-only atomic JSON at <c>&lt;stateRoot&gt;/cowork/&lt;account&gt;/grants.json</c>.</summary>
public sealed class FileCoworkGrantStore : ICoworkGrantStore, IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <param name="stateRoot">The application state root.</param>
    /// <param name="accountId">The account id. It may contain only letters, digits, hyphen and underscore.</param>
    public FileCoworkGrantStore(string stateRoot, string accountId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        _path = System.IO.Path.Combine(CoworkOwnerFiles.AccountDirectory(stateRoot, accountId), "grants.json");
    }

    /// <summary>Gets the file path.</summary>
    public string FilePath => _path;

    /// <inheritdoc />
    public async ValueTask<bool> IsGrantedAsync(CoworkGrantKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        var grants = await ReadAsync(cancellationToken).ConfigureAwait(false);
        return grants.Exists(item => string.Equals(item.Scope, key.Scope, StringComparison.Ordinal) && string.Equals(item.Value, key.Value, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public async ValueTask GrantAsync(CoworkGrantKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await UpdateAsync(
            grants =>
            {
                if (!grants.Exists(item => string.Equals(item.Scope, key.Scope, StringComparison.Ordinal) && string.Equals(item.Value, key.Value, StringComparison.Ordinal)))
                {
                    grants.Add(new CoworkGrantRecord(key.Scope, key.Value));
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask RevokeAsync(CoworkGrantKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await UpdateAsync(
            grants => grants.RemoveAll(item => string.Equals(item.Scope, key.Scope, StringComparison.Ordinal) && string.Equals(item.Value, key.Value, StringComparison.Ordinal)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Releases the write gate.</summary>
    public void Dispose() => _gate.Dispose();

    private async ValueTask<List<CoworkGrantRecord>> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return ReadUnlocked();
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private List<CoworkGrantRecord> ReadUnlocked()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize(json, CoworkToolsJsonContext.Default.ListCoworkGrantRecord) ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private async ValueTask UpdateAsync(Action<List<CoworkGrantRecord>> change, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var grants = ReadUnlocked();
            change(grants);
            var json = JsonSerializer.Serialize(grants, CoworkToolsJsonContext.Default.ListCoworkGrantRecord);
            await CoworkOwnerFiles.WriteAtomicAsync(_path, json, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }
}

using System.Text.Json;

namespace OmpOS.Harness.Automations.Hosted;

/// <summary>
/// Keeps automation secrets in one JSON file that only the owner can read (mode 0600 in a 0700 directory on Unix). The values are NOT
/// encrypted at rest: protect the volume, or register a vault-backed <see cref="IAutomationSecretStore"/> instead. Writes are atomic
/// (see <see cref="AtomicFile"/>). Like the automation store it fails closed on a corrupt file and never overwrites it.
/// </summary>
public sealed class FileAutomationSecretStore : IAutomationSecretStore, IDisposable
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Initializes a new instance of the <see cref="FileAutomationSecretStore"/> class.</summary>
    /// <param name="path">The secret file. Its directory is created on the first write.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty or white space.</exception>
    public FileAutomationSecretStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
    }

    /// <summary>Releases the lock that serializes access.</summary>
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc />
    public async ValueTask<string?> GetAsync(string reference, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var secrets = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return secrets.TryGetValue(reference, out var value) ? value : null;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask SetAsync(string reference, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrEmpty(value);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var secrets = await LoadAsync(cancellationToken).ConfigureAwait(false);
            secrets[reference] = value;
            await WriteAsync(secrets, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(string reference, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var secrets = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (secrets.Remove(reference))
            {
                await WriteAsync(secrets, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async ValueTask WriteAsync(Dictionary<string, string> secrets, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(secrets, FileAutomationSecretStoreJsonContext.Default.DictionaryStringString);
        await AtomicFile.WriteAsync(_path, bytes, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var bytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        try
        {
            var loaded = JsonSerializer.Deserialize(bytes, FileAutomationSecretStoreJsonContext.Default.DictionaryStringString)
                ?? throw new InvalidDataException($"The automation secret file '{_path}' is empty and was not read.");
            return new Dictionary<string, string>(loaded, StringComparer.Ordinal);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The automation secret file '{_path}' is not valid and was not read.", exception);
        }
    }
}

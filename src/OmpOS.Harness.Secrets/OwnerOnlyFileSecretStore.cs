using System.Security.Cryptography;
using System.Text.Json;

namespace OmpOS.Harness.Secrets;

/// <summary>
/// Keeps named secrets in one JSON file as plain text, protected only by file permissions: the directory is created with mode 0700
/// and the file is written with mode 0600 on Unix. This is the same protection omp gives its own credential database; it is not
/// encryption, and anyone who can read the file as its owner, or as root, can read every secret. Use it only where that is an
/// accepted trade-off, and say so wherever the user is told where a key lives.
/// </summary>
/// <remarks>
/// <para>
/// A write goes to a temporary file in the same directory (created with mode 0600, flushed to disk) and replaces the store file by a
/// rename, so a failed write leaves the previous contents in force and no temporary file behind. A file that cannot be read, is not
/// valid JSON, or has another format version is an error (<see cref="SecretStoreException"/>): the store is never reset silently.
/// The file's mode is tightened to 0600 when a read finds it looser. The message of every exception is free of values and contents;
/// <see cref="ToString"/> names the type only.
/// </para>
/// <para>
/// Writers in one process are serialised. There is no lock between processes: two processes that write the same file at the same
/// moment can lose one of the two changes, though the file is never left half written.
/// </para>
/// </remarks>
public sealed class OwnerOnlyFileSecretStore : ISecretStore
{
    private const UnixFileMode OwnerOnlyFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode OwnerOnlyDirectory = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    // One writer at a time per process, across instances, so two stores over the same file also take turns. Static so no field needs disposal.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly string _path;
    private readonly Action<string, string> _replace;

    /// <summary>Creates a store over a file. Nothing is read or written until a method is called.</summary>
    /// <param name="path">The store file, for example <c>secrets.json</c> in the application's state directory. Its directory is created on the first write.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is null, empty or white space.</exception>
    public OwnerOnlyFileSecretStore(string path)
        : this(path, static (temporary, target) => File.Move(temporary, target, overwrite: true))
    {
    }

    /// <summary>Creates a store whose final rename is replaced, so a test can make the replacement fail.</summary>
    /// <param name="path">The store file.</param>
    /// <param name="replace">Moves the finished temporary file (first argument) over the store file (second argument).</param>
    internal OwnerOnlyFileSecretStore(string path, Action<string, string> replace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(replace);
        _path = Path.GetFullPath(path);
        _replace = replace;
    }

    /// <summary>Gets the full path of the store file.</summary>
    public string FilePath => _path;

    /// <inheritdoc />
    public async Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        ValidateName(name);
        ArgumentNullException.ThrowIfNull(secret);
        cancellationToken.ThrowIfCancellationRequested();
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            document.Secrets![name] = secret.Reveal();
            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = Gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
    {
        ValidateName(name);
        cancellationToken.ThrowIfCancellationRequested();
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            return document.Secrets!.TryGetValue(name, out var value) ? new SecretString(value) : null;
        }
        finally
        {
            _ = Gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        ValidateName(name);
        cancellationToken.ThrowIfCancellationRequested();
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            if (!document.Secrets!.Remove(name))
            {
                return false;
            }

            await WriteDocumentAsync(document, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _ = Gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await ReadDocumentAsync(cancellationToken).ConfigureAwait(false);
            string[] names = [.. document.Secrets!.Keys];
            Array.Sort(names, StringComparer.Ordinal);
            return names;
        }
        finally
        {
            _ = Gate.Release();
        }
    }

    /// <summary>Names the type. Neither the path, a secret name nor a value is included.</summary>
    /// <returns>The type name.</returns>
    public override string ToString() => nameof(OwnerOnlyFileSecretStore);

    private static void ValidateName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A secret name must not be empty or whitespace-only.", nameof(name));
        }
    }

    private static async Task WriteTemporaryAsync(string temporary, byte[] bytes, CancellationToken cancellationToken)
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
            options.UnixCreateMode = OwnerOnlyFile;
        }

        var stream = new FileStream(temporary, options);
        await using (stream.ConfigureAwait(false))
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            // .NET has no async flush to disk, so the fsync runs on the thread pool; it must not be cancelled mid-flush.
            await Task.Run(() => stream.Flush(flushToDisk: true), CancellationToken.None).ConfigureAwait(false);
        }
    }

    // Runs only while a write is failing, so the exception already in flight is the one reported. A temporary file that cannot be
    // deleted is left behind; the store reads only its own path, so the leftover file is never used.
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

    private async Task<SecretFileDocument> ReadDocumentAsync(CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return EmptyDocument();
        }
        catch (DirectoryNotFoundException)
        {
            return EmptyDocument();
        }
        catch (IOException exception)
        {
            throw new SecretStoreException("The secret file could not be read.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new SecretStoreException("The secret file could not be read: access was denied.", exception);
        }

        try
        {
            return Parse(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            TightenFileMode();
        }
    }

    private static SecretFileDocument EmptyDocument() =>
        new() { Version = SecretFileDocument.CurrentVersion, Secrets = new Dictionary<string, string>(StringComparer.Ordinal) };

    private static SecretFileDocument Parse(byte[] bytes)
    {
        SecretFileDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(bytes, SecretFileJsonContext.Default.SecretFileDocument);
        }
        catch (JsonException ex)
        {
            // The parser's message is kept out of this one (it can name the offending character of the file); it stays as the inner exception only.
            throw new SecretStoreException("The secret file is not valid JSON. It was left as it is; fix or remove it.", ex);
        }

        if (document is null)
        {
            throw new SecretStoreException("The secret file is empty. It was left as it is; fix or remove it.");
        }

        if (document.Version != SecretFileDocument.CurrentVersion)
        {
            throw new SecretStoreException("The secret file has an unsupported format version. It was left as it is.");
        }

        if (document.Secrets is null)
        {
            throw new SecretStoreException("The secret file has no secret list. It was left as it is; fix or remove it.");
        }

        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in document.Secrets)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(value))
            {
                throw new SecretStoreException("The secret file has an entry without a name or a value. It was left as it is; fix or remove it.");
            }

            secrets[name] = value;
        }

        document.Secrets = secrets;
        return document;
    }

    private void TightenFileMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if ((File.GetUnixFileMode(_path) & ~OwnerOnlyFile) != UnixFileMode.None)
            {
                File.SetUnixFileMode(_path, OwnerOnlyFile);
            }
        }
        catch (IOException)
        {
            // The file vanished or cannot be changed; the next write replaces it with an owner-only file.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: a write creates a new owner-only file and renames it over this one.
        }
    }

    private async Task WriteDocumentAsync(SecretFileDocument document, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, SecretFileJsonContext.Default.SecretFileDocument);
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var replaced = false;
        try
        {
            EnsureDirectory();
            await WriteTemporaryAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            _replace(temporary, _path);
            replaced = true;
        }
        catch (IOException exception)
        {
            throw new SecretStoreException("The secret file could not be written. The previous contents are unchanged.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new SecretStoreException("The secret file could not be written: access was denied. The previous contents are unchanged.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (!replaced)
            {
                DeleteTemporary(temporary);
            }
        }
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        if (Directory.Exists(directory))
        {
            TightenDirectory(directory);
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            _ = Directory.CreateDirectory(directory);
            return;
        }

        _ = Directory.CreateDirectory(directory, OwnerOnlyDirectory);
    }

    // An existing directory is tightened only when it holds nothing but the store file and its temporary files, so a directory that
    // was shared on purpose (the temporary directory, a home directory) is never changed.
    private void TightenDirectory(string directory)
    {
        if (OperatingSystem.IsWindows() || new DirectoryInfo(directory).LinkTarget is not null)
        {
            return;
        }

        var storeName = Path.GetFileName(_path);
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            var name = Path.GetFileName(entry);
            if (!string.Equals(name, storeName, StringComparison.Ordinal)
                && !(name.StartsWith(storeName + ".", StringComparison.Ordinal) && name.EndsWith(".tmp", StringComparison.Ordinal)))
            {
                return;
            }
        }

        if ((File.GetUnixFileMode(directory) & ~OwnerOnlyDirectory) != UnixFileMode.None)
        {
            File.SetUnixFileMode(directory, OwnerOnlyDirectory);
        }
    }
}

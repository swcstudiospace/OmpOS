using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Harness.Secrets.Os;

/// <summary>
/// <see cref="ISecretStore"/> over Windows DPAPI: one file per secret in a directory under the per-user local application data folder. A file is
/// named after the SHA-256 of the secret's name and holds, protected for the current user, the name and the value, so names are listed by
/// reading the files and no file name depends on the length or the case of a name. A write goes to a temporary file in the same directory and
/// replaces the secret's file by one move, so a failed write leaves the previous contents in force. DPAPI protects against other users and
/// offline disk theft; code running as the same user can read every file.
/// </summary>
internal sealed class WindowsDpapiStore : ISecretStore
{
    private const string StoreName = "dpapi";
    private const string StoreDescription = "Windows data protection store";
    private const string FileExtension = ".dpapi";
    private const string TemporaryExtension = ".tmp";

    private static readonly byte[] Magic = "AGD1"u8.ToArray();
    private static readonly UTF8Encoding Strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly string? _directory;
    private readonly IDpapiProtector _protector;
    private readonly ILogger? _logger;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates a store in <c>Autonogrammer\secrets</c> under the per-user local application data folder. Nothing is read or written until a method is called.</summary>
    /// <param name="logger">Receives operation messages. They carry no secret name or value.</param>
    /// <param name="timeProvider">Measures operation durations for the log.</param>
    public WindowsDpapiStore(ILogger? logger = null, TimeProvider? timeProvider = null)
        : this(DefaultDirectory(), new ProtectedDataProtector(), logger, timeProvider)
    {
    }

    /// <summary>Creates a store over a directory and a protector, so a test can use a temporary directory and a reversible fake.</summary>
    /// <param name="directory">The directory of the secret files, or null when the platform has no per-user data folder.</param>
    /// <param name="protector">Encrypts and decrypts the file contents.</param>
    /// <param name="logger">Receives operation messages.</param>
    /// <param name="timeProvider">Measures operation durations for the log.</param>
    internal WindowsDpapiStore(string? directory, IDpapiProtector protector, ILogger? logger, TimeProvider? timeProvider)
    {
        ArgumentNullException.ThrowIfNull(protector);
        _directory = directory;
        _protector = protector;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task SetAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        StoreGuards.ValidateName(name);
        ArgumentNullException.ThrowIfNull(secret);
        _ = await RunAsync("Set", () => WriteAsync(name, secret, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<SecretString?> GetAsync(string name, CancellationToken cancellationToken)
    {
        StoreGuards.ValidateName(name);
        return await RunAsync("Get", () => ReadAsync(name, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveAsync(string name, CancellationToken cancellationToken)
    {
        StoreGuards.ValidateName(name);
        return await RunAsync("Remove", () => Task.FromResult(Delete(name)), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListNamesAsync(CancellationToken cancellationToken) =>
        await RunAsync("List", () => EnumerateAsync(cancellationToken), cancellationToken).ConfigureAwait(false);

    private static string? DefaultDirectory()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrEmpty(local) ? null : Path.Combine(local, "Autonogrammer", "secrets");
    }

    private static string PathFor(string directory, string name) =>
        Path.Combine(directory, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name))) + FileExtension);

    private static byte[] Encode(string name, SecretString secret)
    {
        byte[] nameBytes = Strict.GetBytes(name);
        byte[] valueBytes = Strict.GetBytes(secret.Reveal());
        try
        {
            var plain = new byte[Magic.Length + sizeof(int) + nameBytes.Length + valueBytes.Length];
            Magic.CopyTo(plain, 0);
            BinaryPrimitives.WriteInt32LittleEndian(plain.AsSpan(Magic.Length), nameBytes.Length);
            nameBytes.CopyTo(plain, Magic.Length + sizeof(int));
            valueBytes.CopyTo(plain, Magic.Length + sizeof(int) + nameBytes.Length);
            return plain;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(valueBytes);
        }
    }

    private static (string Name, SecretString Secret) Decode(byte[] plain)
    {
        ReadOnlySpan<byte> span = plain;
        if (span.Length < Magic.Length + sizeof(int) || !span[..Magic.Length].SequenceEqual(Magic))
        {
            throw new InvalidDataException("The stored entry has an unknown format.");
        }

        int nameLength = BinaryPrimitives.ReadInt32LittleEndian(span[Magic.Length..]);
        int nameStart = Magic.Length + sizeof(int);
        if (nameLength < 1 || nameLength > span.Length - nameStart - 1)
        {
            throw new InvalidDataException("The stored entry has an unknown format.");
        }

        try
        {
            string name = Strict.GetString(span.Slice(nameStart, nameLength));
            string value = Strict.GetString(span[(nameStart + nameLength)..]);
            return (name, new SecretString(value));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The stored entry has an unknown format.", exception);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The temporary file is best-effort cleanup after the real failure, which is the one reported.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: the original failure is the one reported.
        }
    }

    private string RequireDirectory() => _directory ?? throw new PlatformNotSupportedException("The platform has no per-user data folder.");

    private async Task<(string Name, SecretString Secret)?> ReadEntryAsync(string path, CancellationToken cancellationToken)
    {
        byte[] protectedBytes;
        try
        {
            protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        byte[] plain = _protector.Unprotect(protectedBytes);
        try
        {
            return Decode(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    private async Task<bool> WriteAsync(string name, SecretString secret, CancellationToken cancellationToken)
    {
        byte[] plain = Encode(name, secret);
        byte[] protectedBytes;
        try
        {
            protectedBytes = _protector.Protect(plain);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        string directory = RequireDirectory();
        _ = Directory.CreateDirectory(directory);
        string target = PathFor(directory, name);
        string temporary = Path.Combine(directory, Path.GetFileNameWithoutExtension(target) + "." + Guid.NewGuid().ToString("N") + TemporaryExtension);
        try
        {
            await File.WriteAllBytesAsync(temporary, protectedBytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, target, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }

        return true;
    }

    private async Task<SecretString?> ReadAsync(string name, CancellationToken cancellationToken)
    {
        string path = PathFor(RequireDirectory(), name);
        if (await ReadEntryAsync(path, cancellationToken).ConfigureAwait(false) is not { } entry)
        {
            return null;
        }

        if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The stored entry does not belong to this name.");
        }

        return entry.Secret;
    }

    private bool Delete(string name)
    {
        string path = PathFor(RequireDirectory(), name);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private async Task<IReadOnlyList<string>> EnumerateAsync(CancellationToken cancellationToken)
    {
        string directory = RequireDirectory();
        if (!Directory.Exists(directory))
        {
            return [];
        }

        List<string> names = [];
        foreach (string path in Directory.EnumerateFiles(directory, "*" + FileExtension))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ReadEntryAsync(path, cancellationToken).ConfigureAwait(false) is { } entry)
            {
                names.Add(entry.Name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private async Task<T> RunAsync<T>(string operation, Func<Task<T>> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long started = _timeProvider.GetTimestamp();
        T result;
        try
        {
            result = await work().ConfigureAwait(false);
        }
        catch (SecretStoreException exception)
        {
            OsStoreLogMessages.OperationFailed(_logger, StoreName, operation, exception.Failure);
            throw;
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or ArgumentException))
        {
            SecretStoreFailure failure = exception switch
            {
                PlatformNotSupportedException => SecretStoreFailure.Unavailable,
                CryptographicException or InvalidDataException => SecretStoreFailure.Corrupt,
                UnauthorizedAccessException => SecretStoreFailure.Denied,
                _ => SecretStoreFailure.Other,
            };
            OsStoreLogMessages.OperationFailed(_logger, StoreName, operation, failure);
            throw new SecretStoreException(StoreGuards.Describe(StoreDescription, failure), failure, exception);
        }

        OsStoreLogMessages.OperationCompleted(_logger, StoreName, operation, (long)_timeProvider.GetElapsedTime(started).TotalMilliseconds);
        return result;
    }
}

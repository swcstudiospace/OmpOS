using System.Security.Cryptography;

namespace OmpOS.Harness.Plugins.Hooks;

/// <summary>
/// Reads hook files from the local file system. A path that is missing or unreadable is reported as absent
/// rather than thrown, so one bad file never stops the others from loading.
/// </summary>
public sealed class LocalHookFileSource : IHookFileSource
{
    private const UnixFileMode GroupOrOtherWritable = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;

    /// <inheritdoc />
    public HookFileInfo Inspect(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Attributes.HasFlag(FileAttributes.Directory))
            {
                return new HookFileInfo(false, 0, false);
            }

            return new HookFileInfo(true, info.Length, IsTamperResistant(info));
        }
        catch (IOException)
        {
            return new HookFileInfo(false, 0, false);
        }
        catch (UnauthorizedAccessException)
        {
            return new HookFileInfo(false, 0, false);
        }
    }

    /// <inheritdoc />
    public async ValueTask<byte[]?> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > maxBytes)
            {
                return null;
            }

            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async ValueTask<string?> DigestAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        var bytes = await ReadAsync(path, maxBytes, cancellationToken).ConfigureAwait(false);
        return bytes is null ? null : Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private static bool IsTamperResistant(FileInfo file)
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        var directory = file.Directory;
        if (directory is null)
        {
            return false;
        }

        return (file.UnixFileMode & GroupOrOtherWritable) == 0 && (directory.UnixFileMode & GroupOrOtherWritable) == 0;
    }
}

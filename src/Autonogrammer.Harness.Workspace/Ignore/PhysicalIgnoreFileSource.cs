using System.Text;

namespace Autonogrammer.Workspace.Ignore;

/// <summary>
/// An <see cref="IIgnoreFileSource"/> over the local disk. It reads only inside the root it was given: a path that climbs out of the root is refused, and a file that is a symbolic link,
/// or lies behind one, is never opened (it reads as missing and <see cref="IsSymbolicLink"/> says why).
/// </summary>
public sealed class PhysicalIgnoreFileSource : IIgnoreFileSource
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _root;
    private readonly string _rootPrefix;

    /// <summary>Creates a source rooted at a workspace directory.</summary>
    /// <param name="root">The workspace root. It need not exist; every read then finds nothing.</param>
    /// <exception cref="ArgumentException"><paramref name="root"/> is null, empty or white space.</exception>
    public PhysicalIgnoreFileSource(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
        _rootPrefix = Path.EndsInDirectorySeparator(_root) ? _root : _root + Path.DirectorySeparatorChar;
    }

    /// <inheritdoc/>
    /// <remarks>An unreadable file (permissions, a race with a delete) reads as missing. A file larger than <paramref name="maxBytes"/> is read only up to <c>maxBytes + 1</c> bytes.</remarks>
    public string? ReadText(string relativePath, long maxBytes)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (!TryResolve(relativePath, out var segments, out var fullPath) || HasLinkOnPath(segments))
        {
            return null;
        }

        try
        {
            return File.Exists(fullPath) ? ReadLimited(fullPath, maxBytes) : null;
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

    /// <inheritdoc/>
    /// <remarks>Also <see langword="true"/> for a path that is empty, rooted or climbs out of the root, because such a path must not be read.</remarks>
    public bool IsSymbolicLink(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        return !TryResolve(relativePath, out var segments, out _) || HasLinkOnPath(segments);
    }

    private static string ReadLimited(string fullPath, long maxBytes)
    {
        var limit = (int)Math.Min(Math.Max(maxBytes, 0) + 1, Array.MaxLength);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var size = (int)Math.Min(limit, stream.Length);
        var buffer = new byte[size];
        var total = 0;
        while (total < size)
        {
            var read = stream.Read(buffer, total, size - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static bool IsLink(string path)
    {
        try
        {
            FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
            return info.LinkTarget is not null;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private bool TryResolve(string relativePath, out string[] segments, out string fullPath)
    {
        fullPath = string.Empty;
        if (!IgnorePath.TryNormalize(relativePath, int.MaxValue, out segments) || segments.Length == 0)
        {
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(_root, Path.Combine(segments)));
        if (!combined.StartsWith(_rootPrefix, PathComparison))
        {
            return false;
        }

        fullPath = combined;
        return true;
    }

    private bool HasLinkOnPath(string[] segments)
    {
        var current = _root;
        foreach (var segment in segments)
        {
            current = Path.Combine(current, segment);
            if (IsLink(current))
            {
                return true;
            }
        }

        return false;
    }
}

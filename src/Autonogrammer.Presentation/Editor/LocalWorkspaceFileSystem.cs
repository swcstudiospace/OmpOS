using System.Text;

namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// The workspace file system over a folder on disk. Listing, reading and writing never leave the root: a path that resolves
/// outside it, through a symbolic link in any ancestor, is refused, and a link whose target lies outside is not listed at all.
/// Links whose target stays inside the root are listed like any other entry, and a write through one replaces the link's target.
/// </summary>
public sealed class LocalWorkspaceFileSystem : IWorkspaceFileSystem
{
    private static readonly StringComparison RootComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // Invalid bytes throw instead of becoming U+FFFD, so a write-back can never corrupt a file that is not UTF-8 text.
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private readonly string _rootFull;

    /// <summary>Opens a workspace over an existing folder.</summary>
    /// <param name="rootDirectory">The folder that becomes the workspace root.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rootDirectory"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    public LocalWorkspaceFileSystem(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);

        _rootFull = Path.GetFullPath(rootDirectory);
        if (!Directory.Exists(_rootFull))
        {
            throw new DirectoryNotFoundException($"The workspace folder '{rootDirectory}' does not exist.");
        }
    }

    /// <inheritdoc/>
    public string RootPath => _rootFull;

    /// <inheritdoc/>
    public IReadOnlyList<WorkspaceEntry> ListDirectory(string relativeDirectory)
    {
        var normalized = WorkspacePath.Normalize(relativeDirectory);
        var directory = Path.GetFullPath(Path.Combine(_rootFull, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(directory) || HasSymlinkEscape(directory))
        {
            throw new UnauthorizedAccessException("The directory resolves outside the workspace root.");
        }

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"No directory exists at '{relativeDirectory}'.");
        }

        var entries = new List<WorkspaceEntry>();
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            var name = Path.GetFileName(path);
            var child = Path.Combine(directory, name);
            if (!IsWithinRoot(child) || HasSymlinkEscape(child))
            {
                continue;
            }

            var relativePath = WorkspacePath.Combine(normalized, name);
            if (Directory.Exists(child))
            {
                entries.Add(new WorkspaceEntry(name, relativePath, IsDirectory: true));
            }
            else if (File.Exists(child))
            {
                entries.Add(new WorkspaceEntry(name, relativePath, IsDirectory: false));
            }
        }

        return entries;
    }

    /// <inheritdoc/>
    public string ReadFile(string relativePath)
    {
        var file = ResolveFile(relativePath);
        return DecodeFile(File.ReadAllBytes(file), out _);
    }

    /// <inheritdoc/>
    public void WriteFile(string relativePath, string content, string expectedCurrent)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(expectedCurrent);

        var file = ResolveFile(relativePath);

        // A link inside the root is written through, so the link itself stays a link.
        var target = new FileInfo(file).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? file;
        var lastWrite = File.GetLastWriteTimeUtc(target);
        var current = DecodeFile(File.ReadAllBytes(target), out var hasBom);
        if (!string.Equals(current, expectedCurrent, StringComparison.Ordinal))
        {
            throw new StaleWorkspaceFileException($"'{relativePath}' changed on disk after it was read; nothing was written.");
        }

        var bytes = StrictUtf8.GetBytes(content);
        var directory = Path.GetDirectoryName(target) ?? _rootFull;
        var temporary = Path.Combine(directory, "." + Path.GetFileName(target) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                if (hasBom)
                {
                    stream.Write(Utf8Bom);
                }

                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsBrowser())
            {
                File.SetUnixFileMode(temporary, File.GetUnixFileMode(target));
            }

            // The file changed between the comparison and the rename: the newer text wins.
            if (File.GetLastWriteTimeUtc(target) != lastWrite)
            {
                throw new StaleWorkspaceFileException($"'{relativePath}' changed on disk while it was being written; nothing was written.");
            }

            File.Move(temporary, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <inheritdoc/>
    public void CreateFile(string relativePath, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalized = WorkspacePath.Normalize(relativePath);
        if (normalized.Length == 0)
        {
            throw new ArgumentException("A file path is required; the workspace root is a directory.", nameof(relativePath));
        }

        var file = Path.GetFullPath(Path.Combine(_rootFull, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(file) || HasSymlinkEscape(file))
        {
            throw new UnauthorizedAccessException("The file resolves outside the workspace root.");
        }

        if (File.Exists(file) || Directory.Exists(file))
        {
            throw new IOException($"'{relativePath}' already exists; nothing was written.");
        }

        var bytes = StrictUtf8.GetBytes(content);
        var directory = Path.GetDirectoryName(file) ?? _rootFull;
        _ = Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "." + Path.GetFileName(file) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            // Without overwrite the move fails when a file appeared in the meantime, so the newer file wins.
            File.Move(temporary, file, overwrite: false);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <inheritdoc/>
    public void DeleteFile(string relativePath, string expectedCurrent)
    {
        ArgumentNullException.ThrowIfNull(expectedCurrent);

        var file = ResolveFile(relativePath);

        // A link inside the root is compared by its target's text, and deleting it removes the link and leaves the target.
        var target = new FileInfo(file).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? file;
        var lastWrite = File.GetLastWriteTimeUtc(target);
        var current = DecodeFile(File.ReadAllBytes(target), out _);
        if (!string.Equals(current, expectedCurrent, StringComparison.Ordinal))
        {
            throw new StaleWorkspaceFileException($"'{relativePath}' changed on disk after it was read; nothing was deleted.");
        }

        if (File.GetLastWriteTimeUtc(target) != lastWrite)
        {
            throw new StaleWorkspaceFileException($"'{relativePath}' changed on disk while it was being deleted; nothing was deleted.");
        }

        File.Delete(file);
    }

    private bool IsWithinRoot(string candidate)
    {
        if (candidate.Length < _rootFull.Length || !candidate.StartsWith(_rootFull, RootComparison))
        {
            return false;
        }

        // A filesystem root such as "/" already ends with a separator, so the character after it is the child's first.
        return candidate.Length == _rootFull.Length
            || Path.EndsInDirectorySeparator(_rootFull)
            || candidate[_rootFull.Length] == Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Checks the path and every existing ancestor down to the root for a link whose final target leaves the root.
    /// Ancestors that do not exist yet cannot be links.
    /// </summary>
    private bool HasSymlinkEscape(string fullPath)
    {
        var current = fullPath;
        while (current is not null
            && !string.Equals(current, _rootFull, RootComparison)
            && IsWithinRoot(current))
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists)
            {
                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is not null && !IsWithinRoot(Path.GetFullPath(target.FullName)))
                {
                    return true;
                }
            }

            current = Path.GetDirectoryName(current);
        }

        return false;
    }

    // The absolute path of an existing file inside the root. A path that leaves the root is refused before the disk is touched.
    private string ResolveFile(string relativePath)
    {
        var normalized = WorkspacePath.Normalize(relativePath);
        if (normalized.Length == 0)
        {
            throw new ArgumentException("A file path is required; the workspace root is a directory.", nameof(relativePath));
        }

        var file = Path.GetFullPath(Path.Combine(_rootFull, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinRoot(file) || HasSymlinkEscape(file))
        {
            throw new UnauthorizedAccessException("The file resolves outside the workspace root.");
        }

        if (!File.Exists(file))
        {
            throw new FileNotFoundException($"No file exists at '{relativePath}'.", file);
        }

        return file;
    }

    private static string DecodeFile(byte[] bytes, out bool hasBom)
    {
        hasBom = bytes.AsSpan().StartsWith(Utf8Bom);
        try
        {
            return StrictUtf8.GetString(bytes, hasBom ? Utf8Bom.Length : 0, bytes.Length - (hasBom ? Utf8Bom.Length : 0));
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The file is not valid UTF-8 text.", exception);
        }
    }
}

using System.Collections.Frozen;
using OmpOS.Presentation.Mentions;
using OmpOS.Workspace.Ignore;

namespace OmpOS.Native.Composition.Mentions;

/// <summary>
/// The workspace tree and file reads of a local workspace, for the file and folder mentions. The tree is walked breadth first from the root and kept for
/// <see cref="CacheLifetime"/>, so typing in the picker does not walk the disk on every key. The walk skips <c>.git</c>, <c>node_modules</c>, <c>bin</c> and <c>obj</c>,
/// never follows a symbolic link (a link is neither listed nor readable), stops at <see cref="MaxEntries"/> entries, and leaves out what the ignore matcher excludes
/// (an ignored folder is not entered). Reads refuse a binary file, a missing file, a symbolic link and any path outside the root with <see cref="MentionUnavailableException"/>.
/// </summary>
/// <remarks>
/// The ignore files of a directory are read by the walk, through <see cref="IgnoreMatcher.TryLoadHierarchy"/>, just before the directory is listed: the root always, the
/// folders below it only when <c>hierarchicalIgnore</c> is set. A directory is loaded once per instance, so an ignore file edited later is read by the next session, not by the next listing.
/// Every call this class makes on the matcher locks the matcher; <see cref="IgnoreMatcherFilter"/> does the same, which is how the walk and the picker may share one matcher.
/// Nothing touches the disk until the first <see cref="ListAsync"/>, <see cref="ReadAsync"/>.
/// </remarks>
internal sealed class WorkspaceMentionFiles : IMentionFileSource
{
    /// <summary>The most entries a listing holds.</summary>
    internal const int MaxEntries = 20_000;

    /// <summary>How many leading bytes of a file are checked for a NUL byte, which marks the file as binary.</summary>
    internal const int BinarySniffBytes = 8 * 1024;

    private static readonly FrozenSet<string> SkippedDirectories = new[] { ".git", "node_modules", "bin", "obj" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _root;
    private readonly string _rootPrefix;
    private readonly IgnoreMatcher? _ignore;
    private readonly PhysicalIgnoreFileSource? _ignoreFiles;
    private readonly bool _hierarchicalIgnore;
    private readonly int _maxEntries;
    private readonly TimeProvider _timeProvider;
    private readonly HashSet<string> _loadedDirectories = new(StringComparer.Ordinal);
    private readonly System.Threading.Lock _gate = new();
    private Task<IReadOnlyList<MentionFileEntry>>? _listing;
    private DateTimeOffset _listedAt;

    /// <summary>Creates the source. It reads nothing until it is used.</summary>
    /// <param name="root">The workspace root. It need not exist; the listing is then empty and every read is unavailable.</param>
    /// <param name="ignore">The matcher that decides which entries are left out, or <see langword="null"/> for none. The walk loads the ignore files into it.</param>
    /// <param name="hierarchicalIgnore">Whether the ignore files below the root are loaded as well (the root's are always).</param>
    /// <param name="timeProvider">The clock of the listing cache, or <see langword="null"/> for the system clock.</param>
    /// <param name="maxEntries">The most entries a listing holds, <see cref="MaxEntries"/> by default.</param>
    /// <exception cref="ArgumentException"><paramref name="root"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxEntries"/> is not positive.</exception>
    internal WorkspaceMentionFiles(
        string root,
        IgnoreMatcher? ignore = null,
        bool hierarchicalIgnore = true,
        TimeProvider? timeProvider = null,
        int maxEntries = MaxEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);

        _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        _rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        _ignore = ignore;
        _ignoreFiles = ignore is null ? null : new PhysicalIgnoreFileSource(_root);
        _hierarchicalIgnore = hierarchicalIgnore;
        _maxEntries = maxEntries;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets how long a listing is kept before the next call walks the tree again.</summary>
    internal static TimeSpan CacheLifetime { get; } = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public Task<IReadOnlyList<MentionFileEntry>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task<IReadOnlyList<MentionFileEntry>> listing;
        lock (_gate)
        {
            if (_listing is null || (_listing.IsCompleted && (_listing.Status != TaskStatus.RanToCompletion || _timeProvider.GetUtcNow() - _listedAt >= CacheLifetime)))
            {
                _listing = Task.Run(WalkAndStamp, CancellationToken.None);
            }

            listing = _listing;
        }

        return listing.WaitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="MentionUnavailableException">The file is missing, binary, a symbolic link, unreadable, or outside the workspace.</exception>
    public async Task<MentionContent> ReadAsync(string relativePath, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        var path = ResolveFile(relativePath);
        try
        {
            using var stream = new FileStream(
                path,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
                });
            var length = stream.Length;
            var wanted = (int)Math.Min(length, Math.Max(maxBytes, BinarySniffBytes));
            var buffer = new byte[wanted];
            var read = await stream.ReadAtLeastAsync(buffer, wanted, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
            if (buffer.AsSpan(0, Math.Min(read, BinarySniffBytes)).Contains((byte)0))
            {
                throw new MentionUnavailableException("'" + relativePath + "' is a binary file and cannot be attached.");
            }

            var kept = Math.Max(0, Math.Min(read, maxBytes));
            return new MentionContent(MentionTextCut.DecodeHead(buffer.AsSpan(0, kept)), length, length > kept);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new MentionUnavailableException("'" + relativePath + "' cannot be read.", exception);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> is rooted or reaches outside the workspace.</exception>
    public Uri ToUri(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        if (!TryResolve(relativePath, out var full))
        {
            throw new ArgumentException("The path is outside the workspace.", nameof(relativePath));
        }

        return new Uri(full, UriKind.Absolute);
    }

    private static bool IsLink(FileSystemInfo info) => info.Attributes.HasFlag(FileAttributes.ReparsePoint) && info.LinkTarget is not null;

    private IReadOnlyList<MentionFileEntry> WalkAndStamp()
    {
        var entries = Walk();
        lock (_gate)
        {
            _listedAt = _timeProvider.GetUtcNow();
        }

        return entries;
    }

    private List<MentionFileEntry> Walk()
    {
        var entries = new List<MentionFileEntry>();
        var pending = new Queue<string>();
        pending.Enqueue(string.Empty);
        while (pending.Count > 0 && entries.Count < _maxEntries)
        {
            ListDirectory(pending.Dequeue(), entries, pending);
        }

        return entries;
    }

    private void ListDirectory(string relative, List<MentionFileEntry> entries, Queue<string> pending)
    {
        LoadIgnoreFiles(relative);

        List<FileSystemInfo> children;
        try
        {
            var directory = relative.Length == 0 ? _root : Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
            children = [.. new DirectoryInfo(directory).EnumerateFileSystemInfos().OrderBy(static child => child.Name, StringComparer.Ordinal)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var child in children)
        {
            if (entries.Count >= _maxEntries)
            {
                return;
            }

            var isDirectory = child is DirectoryInfo;
            if (isDirectory ? SkippedDirectories.Contains(child.Name) : string.Equals(child.Name, ".git", StringComparison.Ordinal))
            {
                continue;
            }

            var path = relative.Length == 0 ? child.Name : relative + "/" + child.Name;
            if (IsLink(child) || IsIgnored(path, isDirectory))
            {
                continue;
            }

            if (isDirectory)
            {
                entries.Add(new MentionFileEntry(path, true, 0));
                pending.Enqueue(path);
            }
            else if (TryGetLength(child, out var length))
            {
                entries.Add(new MentionFileEntry(path, false, length));
            }
        }
    }

    private static bool TryGetLength(FileSystemInfo child, out long length)
    {
        try
        {
            length = child is FileInfo file ? file.Length : 0L;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            length = 0;
            return false;
        }
    }

    private void LoadIgnoreFiles(string relative)
    {
        if (_ignore is null || _ignoreFiles is null || (relative.Length > 0 && !_hierarchicalIgnore) || !_loadedDirectories.Add(relative))
        {
            return;
        }

        lock (_ignore)
        {
            _ignore.TryLoadHierarchy(_ignoreFiles, relative);
        }
    }

    private bool IsIgnored(string path, bool isDirectory)
    {
        if (_ignore is null)
        {
            return false;
        }

        lock (_ignore)
        {
            return _ignore.IsIgnored(path, isDirectory);
        }
    }

    private bool TryResolve(string relativePath, out string full)
    {
        full = string.Empty;
        if (relativePath.Length == 0 || relativePath.Contains('\0', StringComparison.Ordinal) || Path.IsPathRooted(relativePath))
        {
            return false;
        }

        var combined = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(_rootPrefix, PathComparison) && !string.Equals(combined, _root, PathComparison))
        {
            return false;
        }

        full = combined;
        return true;
    }

    private string ResolveFile(string relativePath)
    {
        if (!TryResolve(relativePath, out var full) || string.Equals(full, _root, PathComparison))
        {
            throw new MentionUnavailableException("'" + relativePath + "' is outside the workspace and cannot be attached.");
        }

        if (HasLinkOnPath(full))
        {
            throw new MentionUnavailableException("'" + relativePath + "' is a symbolic link and cannot be attached.");
        }

        if (!File.Exists(full))
        {
            throw new MentionUnavailableException("'" + relativePath + "' no longer exists in the workspace.");
        }

        return full;
    }

    private bool HasLinkOnPath(string full)
    {
        var current = _root;
        foreach (var segment in full[_rootPrefix.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (!info.Exists)
            {
                return false;
            }

            if (IsLink(info))
            {
                return true;
            }
        }

        return false;
    }
}

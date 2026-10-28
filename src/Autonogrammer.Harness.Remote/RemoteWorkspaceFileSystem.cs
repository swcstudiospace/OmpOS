using System.Globalization;
using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// The workspace files of an ssh host, for the file tree, the editor and the diff review: <see cref="IAsyncWorkspaceFileSystem"/> over short
/// POSIX scripts run through <see cref="IRemoteExec"/>. Nothing leaves the remote workspace root: the remote machine resolves the real path of
/// every path with <c>realpath</c> (or <c>readlink -f</c>, or <c>cd -P</c>) before it touches it, so a symbolic link that leads outside is refused
/// and not listed. A read returns at most the cap the caller gives (1 MiB by default) and refuses a binary file. A write, create or delete runs
/// as one remote script that compares the file signature (modification time and size) it read with the one on disk just before it replaces the
/// file by renaming a temporary file from the same directory, so a reader sees the old or the new text and a concurrent change wins.
/// </summary>
public sealed class RemoteWorkspaceFileSystem : IAsyncWorkspaceFileSystem
{
    /// <summary>The most entries one directory listing returns; a larger directory is cut at this count.</summary>
    public const int MaxEntriesPerDirectory = 5000;

    /// <summary>The most text bytes a write or create sends.</summary>
    public const int MaxWriteBytes = 8 * 1024 * 1024;

    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    private readonly IRemoteExec _exec;
    private readonly string _root;

    /// <summary>Initializes a new instance of the <see cref="RemoteWorkspaceFileSystem"/> class over the remote workspace root of the exec's profile.</summary>
    /// <param name="exec">Runs the scripts on the remote machine.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exec"/> is null.</exception>
    public RemoteWorkspaceFileSystem(IRemoteExec exec)
    {
        ArgumentNullException.ThrowIfNull(exec);
        _exec = exec;
        _root = RemotePaths.TrimTrailingSlash(exec.WorkspaceRoot);
    }

    /// <inheritdoc/>
    public string RootPath => _root;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WorkspaceEntry>> ListDirectoryAsync(string relativeDirectory, CancellationToken cancellationToken)
    {
        var relative = WorkspacePath.Normalize(relativeDirectory);
        var script = RemoteScripts.List(_root, RemotePaths.Join(_root, relative), MaxEntriesPerDirectory, verifyLinks: true);
        var result = await _exec.RunAsync(script, ReadOnlyMemory<byte>.Empty, RemoteExecLimits.ListingBytes, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw RemoteFileErrors.For(result, relative, RemoteFileTarget.Directory);
        }

        var (entries, _) = RemoteFileParsing.ParseList(result.OutputText());
        var listed = new List<WorkspaceEntry>(entries.Count);
        foreach (var (name, isDirectory, _) in entries)
        {
            if (WorkspacePath.IsPlainName(name))
            {
                listed.Add(new WorkspaceEntry(name, WorkspacePath.Combine(relative, name), isDirectory));
            }
        }

        return listed;
    }

    /// <inheritdoc/>
    public async Task<string> ReadFileAsync(string relativePath, int maxBytes, CancellationToken cancellationToken)
    {
        var read = await ReadSignedAsync(RequireFile(relativePath), maxBytes, cancellationToken).ConfigureAwait(false);
        return read.Text;
    }

    /// <inheritdoc/>
    public async Task<WorkspaceFileStat?> StatAsync(string relativePath, CancellationToken cancellationToken)
    {
        var relative = WorkspacePath.Normalize(relativePath);
        var script = RemoteScripts.Stat(_root, RemotePaths.Join(_root, relative));
        var result = await _exec.RunAsync(script, ReadOnlyMemory<byte>.Empty, RemoteExecLimits.SmallAnswerBytes, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw RemoteFileErrors.For(result, relative, RemoteFileTarget.File);
        }

        if (!RemoteFileParsing.TryParseStat(result.OutputText(), out var kind, out var length, out var modified))
        {
            throw new IOException("The remote machine gave no answer for '" + relative + "'.");
        }

        return kind switch
        {
            "dir" => new WorkspaceFileStat(IsDirectory: true, Length: null, modified),
            "file" => new WorkspaceFileStat(IsDirectory: false, length, modified),
            _ => null,
        };
    }

    /// <inheritdoc/>
    public async Task WriteFileAsync(string relativePath, string content, string expectedCurrent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(expectedCurrent);
        var relative = RequireFile(relativePath);
        var current = await ReadSignedAsync(relative, WorkspaceFileLimits.DefaultMaxReadBytes, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(current.Text, expectedCurrent, StringComparison.Ordinal))
        {
            throw new StaleWorkspaceFileException($"'{relative}' changed on the remote machine after it was read; nothing was written.");
        }

        var bytes = Encode(content, current.HasByteOrderMark);
        var script = RemoteScripts.Replace(_root, RemotePaths.Join(_root, relative), current.Signature);
        var result = await _exec.RunAsync(script, bytes, RemoteExecLimits.SmallAnswerBytes, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw RemoteFileErrors.For(result, relative, RemoteFileTarget.File);
        }
    }

    /// <inheritdoc/>
    public async Task CreateFileAsync(string relativePath, string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var relative = RequireFile(relativePath);
        var bytes = Encode(content, withByteOrderMark: false);
        var script = RemoteScripts.Create(_root, RemotePaths.Join(_root, relative));
        var result = await _exec.RunAsync(script, bytes, RemoteExecLimits.SmallAnswerBytes, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw RemoteFileErrors.For(result, relative, RemoteFileTarget.File);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteFileAsync(string relativePath, string expectedCurrent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedCurrent);
        var relative = RequireFile(relativePath);
        var current = await ReadSignedAsync(relative, WorkspaceFileLimits.DefaultMaxReadBytes, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(current.Text, expectedCurrent, StringComparison.Ordinal))
        {
            throw new StaleWorkspaceFileException($"'{relative}' changed on the remote machine after it was read; nothing was deleted.");
        }

        var script = RemoteScripts.Delete(_root, RemotePaths.Join(_root, relative), current.Signature);
        var result = await _exec.RunAsync(script, ReadOnlyMemory<byte>.Empty, RemoteExecLimits.SmallAnswerBytes, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw RemoteFileErrors.For(result, relative, RemoteFileTarget.File);
        }
    }

    private static string RequireFile(string relativePath)
    {
        var relative = WorkspacePath.Normalize(relativePath);
        if (relative.Length == 0)
        {
            throw new ArgumentException("A file path is required; the workspace root is a directory.", nameof(relativePath));
        }

        return relative;
    }

    private static byte[] Encode(string content, bool withByteOrderMark)
    {
        var body = RemoteFileParsing.Utf8.GetBytes(content);
        if (body.Length > MaxWriteBytes)
        {
            throw new WorkspaceFileTooLargeException(string.Create(CultureInfo.InvariantCulture, $"The text holds more than {MaxWriteBytes} bytes."));
        }

        if (!withByteOrderMark)
        {
            return body;
        }

        var bytes = new byte[Utf8Bom.Length + body.Length];
        Utf8Bom.CopyTo(bytes);
        body.CopyTo(bytes.AsSpan(Utf8Bom.Length));
        return bytes;
    }

    private async Task<SignedRead> ReadSignedAsync(string relative, int maxBytes, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        var script = RemoteScripts.ReadWithSignature(_root, RemotePaths.Join(_root, relative), maxBytes);
        var result = await _exec.RunAsync(script, ReadOnlyMemory<byte>.Empty, maxBytes + 1 + RemoteFileParsing.SignatureRoom, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw RemoteFileErrors.For(result, relative, RemoteFileTarget.File);
        }

        if (!RemoteFileParsing.TrySplitSignedRead(result.Output, out var signature, out var content))
        {
            throw new IOException("The remote machine gave no answer for '" + relative + "'.");
        }

        if (content.Length > maxBytes)
        {
            throw new WorkspaceFileTooLargeException(string.Create(CultureInfo.InvariantCulture, $"'{relative}' holds more than {maxBytes} bytes."));
        }

        var text = RemoteFileParsing.DecodeText(content.Span, out var hasByteOrderMark);
        return new SignedRead(text, hasByteOrderMark, signature);
    }

    private readonly record struct SignedRead(string Text, bool HasByteOrderMark, string Signature);
}

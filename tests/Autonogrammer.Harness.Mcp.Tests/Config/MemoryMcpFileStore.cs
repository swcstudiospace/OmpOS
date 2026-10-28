using System.Text;
using Autonogrammer.Harness.Mcp.Config;

namespace Autonogrammer.Harness.Mcp.Tests.Config;

/// <summary>An in-memory file tree for the loader and the writer: files by absolute path, with oversize and unreadable files on demand.</summary>
internal sealed class MemoryMcpFileStore : IMcpFileStore
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);

    /// <summary>Gets the path of every write, in order.</summary>
    public List<string> Writes { get; } = [];

    /// <summary>Gets or sets a failure the next write throws.</summary>
    public IOException? WriteFailure { get; set; }

    /// <summary>Adds a UTF-8 text file.</summary>
    /// <param name="path">The absolute path.</param>
    /// <param name="text">The content.</param>
    /// <returns>This store.</returns>
    public MemoryMcpFileStore With(string path, string text)
    {
        _files[path] = new UTF8Encoding(false).GetBytes(text);
        return this;
    }

    /// <summary>Adds a file of raw bytes.</summary>
    /// <param name="path">The absolute path.</param>
    /// <param name="bytes">The content.</param>
    /// <returns>This store.</returns>
    public MemoryMcpFileStore WithBytes(string path, byte[] bytes)
    {
        _files[path] = bytes;
        return this;
    }

    /// <summary>Makes a path exist but fail to read.</summary>
    /// <param name="path">The absolute path.</param>
    /// <returns>This store.</returns>
    public MemoryMcpFileStore WithUnreadable(string path)
    {
        _ = _unreadable.Add(path);
        return this;
    }

    /// <summary>Returns a file's text.</summary>
    /// <param name="path">The absolute path.</param>
    /// <returns>The content.</returns>
    public string Text(string path) => Encoding.UTF8.GetString(_files[path]);

    /// <summary>Returns whether a file exists.</summary>
    /// <param name="path">The absolute path.</param>
    /// <returns>True when the file exists.</returns>
    public bool Exists(string path) => _files.ContainsKey(path);

    /// <inheritdoc />
    public ValueTask<McpFileRead> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        if (_unreadable.Contains(path))
        {
            return ValueTask.FromResult(McpFileRead.Unreadable);
        }

        if (!_files.TryGetValue(path, out var bytes))
        {
            return ValueTask.FromResult(McpFileRead.Missing);
        }

        return ValueTask.FromResult(bytes.Length > maxBytes ? McpFileRead.TooLarge : McpFileRead.Read(bytes));
    }

    /// <inheritdoc />
    public Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        if (WriteFailure is not null)
        {
            throw WriteFailure;
        }

        Writes.Add(path);
        _ = With(path, content);
        return Task.CompletedTask;
    }
}

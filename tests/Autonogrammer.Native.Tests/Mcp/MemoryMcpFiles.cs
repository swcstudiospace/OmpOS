using System.Text;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;

namespace Autonogrammer.Native.Tests.Mcp;

/// <summary>An in-memory file tree and state store for the catalog adapter tests.</summary>
internal sealed class MemoryMcpFiles : IMcpFileStore, IMcpStateStore
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private McpState _state = McpState.Empty;

    /// <summary>Adds a UTF-8 text file.</summary>
    /// <param name="path">The absolute path.</param>
    /// <param name="text">The content.</param>
    /// <returns>This tree.</returns>
    public MemoryMcpFiles With(string path, string text)
    {
        _files[path] = Encoding.UTF8.GetBytes(text);
        return this;
    }

    /// <summary>Returns the text of a file, as the adapter wrote it.</summary>
    /// <param name="path">The absolute path.</param>
    /// <returns>The content, or <see langword="null"/> when the file does not exist.</returns>
    public string? Text(string path) => _files.TryGetValue(path, out var bytes) ? Encoding.UTF8.GetString(bytes) : null;

    /// <inheritdoc />
    public ValueTask<McpFileRead> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_files.TryGetValue(path, out var bytes) ? McpFileRead.Read(bytes) : McpFileRead.Missing);

    /// <inheritdoc />
    public Task WriteTextAsync(string path, string content, CancellationToken cancellationToken)
    {
        _ = With(path, content);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<McpState> LoadAsync(CancellationToken cancellationToken) => Task.FromResult(_state);

    /// <inheritdoc />
    public Task SaveAsync(McpState state, CancellationToken cancellationToken)
    {
        _state = state;
        return Task.CompletedTask;
    }
}

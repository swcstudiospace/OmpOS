using System.Text;
using OmpOS.Harness.Mcp.Catalog;
using OmpOS.Harness.Mcp.Config;

namespace OmpOS.Native.Tests.Mcp;

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

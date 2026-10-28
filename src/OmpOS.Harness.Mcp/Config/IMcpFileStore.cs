namespace OmpOS.Harness.Mcp.Config;

/// <summary>
/// The file tree MCP configuration is read from and the OmpOS-owned file is written to. A local disk, an SSH host and a
/// test fixture implement it, so the same loader serves all of them.
/// </summary>
public interface IMcpFileStore
{
    /// <summary>Reads at most <paramref name="maxBytes"/> bytes of a file. It never throws for a missing, oversize or unreadable file.</summary>
    /// <param name="path">The file path in this store's path form.</param>
    /// <param name="maxBytes">The most bytes to read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>What happened, and the bytes when the file was read.</returns>
    ValueTask<McpFileRead> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken);

    /// <summary>Replaces a file with UTF-8 text, creating the directory. The replacement is atomic where the store can make it so.</summary>
    /// <param name="path">The file path in this store's path form.</param>
    /// <param name="content">The new content.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    /// <exception cref="IOException">The file cannot be written.</exception>
    Task WriteTextAsync(string path, string content, CancellationToken cancellationToken);
}

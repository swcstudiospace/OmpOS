using Autonogrammer.Harness.Mcp;
using Autonogrammer.Harness.Mcp.Catalog;
using Autonogrammer.Harness.Mcp.Config;

namespace Autonogrammer.Native.Composition.Connectors;

/// <summary>
/// Decorates a connection checker and remembers the last tool list for each server name. The catalog still sees the original result.
/// </summary>
public sealed class RecordingConnectionChecker : IMcpConnectionChecker
{
    private readonly IMcpConnectionChecker _inner;
    private readonly Dictionary<string, IReadOnlyList<McpToolDescriptor>> _tools = new(StringComparer.Ordinal);

    /// <summary>Creates a recorder over <paramref name="inner"/>.</summary>
    /// <param name="inner">The checker that connects. A check runs only when this method's caller asks.</param>
    public RecordingConnectionChecker(IMcpConnectionChecker inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <inheritdoc />
    public async Task<McpConnectionResult> CheckAsync(McpServerSpec server, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);
        var result = await _inner.CheckAsync(server, cancellationToken).ConfigureAwait(false);
        _tools[server.Name] = result.Tools ?? [];
        return result;
    }

    /// <summary>Returns the tool names remembered for a server, or an empty list when that server has not been checked.</summary>
    /// <param name="serverName">The server name.</param>
    /// <returns>The tool names.</returns>
    public IReadOnlyList<string> ToolNames(string serverName) =>
        _tools.TryGetValue(serverName, out var tools) ? [.. tools.Select(tool => tool.Id.ToolName)] : [];
}

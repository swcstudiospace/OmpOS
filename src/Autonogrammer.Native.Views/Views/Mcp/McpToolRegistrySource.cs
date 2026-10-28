using Autonogrammer.Harness.Mcp;

namespace Autonogrammer.Native.Views.Mcp;

/// <summary>Adapts the production <see cref="McpToolRegistry"/> to the window's <see cref="IMcpRegistryHost"/>.</summary>
internal sealed class McpToolRegistrySource(McpToolRegistry registry) : IMcpRegistryHost
{
    private readonly McpToolRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    /// <inheritdoc />
    public IReadOnlyList<McpToolDescriptor> Tools => _registry.Tools;

    /// <inheritdoc />
    public IReadOnlyList<McpToolDescriptor> GetOfferedTools(string sessionId) => _registry.GetOfferedTools(sessionId);

    /// <inheritdoc />
    public bool IsEnabled(string sessionId, McpToolId tool) => _registry.IsEnabled(sessionId, tool);

    /// <inheritdoc />
    public void Enable(string sessionId, McpToolId tool) => _registry.Enable(sessionId, tool);

    /// <inheritdoc />
    public void Disable(string sessionId, McpToolId tool) => _registry.Disable(sessionId, tool);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _registry.DisposeAsync();
}

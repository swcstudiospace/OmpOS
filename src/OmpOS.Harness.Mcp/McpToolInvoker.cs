using ModelContextProtocol.Protocol;

namespace OmpOS.Harness.Mcp;

/// <summary>
/// The only route from a model's tool call to an MCP server. A call must name a tool enabled for its
/// session, then pass the injected approval decision; only then is the server contacted.
/// </summary>
public sealed class McpToolInvoker
{
    private readonly McpToolRegistry _registry;
    private readonly McpToolApproval _approve;

    /// <summary>Creates an invoker over a registry.</summary>
    /// <param name="registry">The registry whose enabled tools may be called.</param>
    /// <param name="approve">The approval decision for each call.</param>
    public McpToolInvoker(McpToolRegistry registry, McpToolApproval approve)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(approve);
        _registry = registry;
        _approve = approve;
    }

    /// <summary>Calls an enabled tool after it is approved.</summary>
    /// <param name="sessionId">The session making the call.</param>
    /// <param name="tool">The tool to call.</param>
    /// <param name="arguments">The arguments for the tool.</param>
    /// <param name="cancellationToken">Cancels the approval or the call.</param>
    /// <returns>The server's result, including any tool-level error the server reported.</returns>
    /// <exception cref="McpToolNotOfferedException">The tool is unknown or disabled for the session; no approval is requested.</exception>
    /// <exception cref="McpToolDeniedException">The approval decision refused the call; the server is not contacted.</exception>
    public async ValueTask<CallToolResult> InvokeAsync(
        string sessionId,
        McpToolId tool,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(arguments);

        var offered = _registry.ResolveOffered(sessionId, tool);
        var request = new McpToolCallRequest(sessionId, tool, arguments);
        if (!await _approve(request, cancellationToken).ConfigureAwait(false))
        {
            throw new McpToolDeniedException(sessionId, tool);
        }

        return await offered.Client
            .CallToolAsync(tool.ToolName, arguments, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }
}

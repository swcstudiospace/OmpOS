namespace OmpOS.Harness.Mcp;

/// <summary>
/// Decides whether one MCP tool call may reach its server. Returns <c>true</c> to approve.
/// Injected by the host; the ACP terminal or approval gate supplies it in production.
/// </summary>
/// <param name="request">The call awaiting a decision.</param>
/// <param name="cancellationToken">Cancels the decision.</param>
/// <returns><c>true</c> when the call is approved.</returns>
public delegate ValueTask<bool> McpToolApproval(McpToolCallRequest request, CancellationToken cancellationToken);

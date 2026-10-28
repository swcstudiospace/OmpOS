namespace OmpOS.Harness.Mcp.Catalog;

/// <summary>What a connection check found.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Reason">One sentence for a failure. The catalog removes every environment and header value from it and cuts it short, but a checker should not include one.</param>
/// <param name="ToolCount">How many tools the server listed, when it connected.</param>
public sealed record McpConnectionResult(McpConnectionOutcome Outcome, string? Reason = null, int ToolCount = 0);

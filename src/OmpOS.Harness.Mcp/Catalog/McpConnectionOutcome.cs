namespace OmpOS.Harness.Mcp.Catalog;

/// <summary>The result of a connection check.</summary>
public enum McpConnectionOutcome
{
    /// <summary>The server answered the MCP handshake and listed its tools.</summary>
    Connected = 0,

    /// <summary>The server could not be reached or answered with an error.</summary>
    Failed = 1,

    /// <summary>The server answered that it needs sign-in (HTTP 401 or an OAuth challenge).</summary>
    AuthRequired = 2,
}

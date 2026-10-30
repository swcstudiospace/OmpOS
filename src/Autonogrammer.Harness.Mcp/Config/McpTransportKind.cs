namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>How omp reaches an MCP server.</summary>
public enum McpTransportKind
{
    /// <summary>omp starts the server as a child process and talks to it over stdio.</summary>
    Stdio = 0,

    /// <summary>The server is reached over Streamable HTTP.</summary>
    Http = 1,

    /// <summary>The server is reached over legacy server-sent events.</summary>
    Sse = 2,
}

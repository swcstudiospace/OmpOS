namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>The dialect of an MCP configuration file. It decides the top-level key and the variable syntax.</summary>
public enum McpConfigFormat
{
    /// <summary>Autonogrammer's own <c>.autonogrammer/mcp.json</c>: a <c>mcpServers</c> object, Cursor variable syntax.</summary>
    Autonogrammer = 0,

    /// <summary>Cursor's <c>.cursor/mcp.json</c> and <c>~/.cursor/mcp.json</c>: a <c>mcpServers</c> object, Cursor variable syntax.</summary>
    Cursor = 1,

    /// <summary>Claude Code's project file <c>.mcp.json</c>: a <c>mcpServers</c> object, <c>${VAR}</c> and <c>${VAR:-default}</c> expansion.</summary>
    ClaudeProject = 2,

    /// <summary>Claude Code's user file <c>~/.claude.json</c>: the top-level <c>mcpServers</c> object of a larger document.</summary>
    ClaudeUser = 3,

    /// <summary>VS Code's <c>.vscode/mcp.json</c>: a <c>servers</c> object (comments allowed), Cursor-like variable syntax.</summary>
    ServersObject = 4,
}

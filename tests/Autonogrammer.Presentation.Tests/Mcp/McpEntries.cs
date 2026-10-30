using Autonogrammer.Presentation.Mcp;

namespace Autonogrammer.Presentation.Tests.Mcp;

/// <summary>Builders for the server entries the MCP view model tests use.</summary>
internal static class McpEntries
{
    /// <summary>A user stdio server that is ready.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The entry.</returns>
    public static McpServerEntry Ready(string name = "files") =>
        new(name, "stdio", McpEntryScope.User, "~/.cursor/mcp.json", McpServerStatusKind.Configured, null, null, "npx -y files-mcp", ["API_KEY"], [], true, true, false, false, false);

    /// <summary>A project stdio server that waits for approval.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The entry.</returns>
    public static McpServerEntry NeedsConsent(string name = "tool") =>
        new(name, "stdio", McpEntryScope.Project, ".cursor/mcp.json", McpServerStatusKind.NeedsConsent, null, null, "node ./tool.js --serve", ["TOOL_TOKEN"], [], true, false, true, false, false);

    /// <summary>A project remote server whose variable is not set.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The entry.</returns>
    public static McpServerEntry Failed(string name = "search") =>
        new(name, "http", McpEntryScope.Project, ".cursor/mcp.json", McpServerStatusKind.Failed, "Not set: environment variable SEARCH_TOKEN.", null, "https://mcp.example.com", [], ["Authorization"], true, false, true, false, false);

    /// <summary>A user remote server that asked for sign-in.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The entry.</returns>
    public static McpServerEntry AuthRequired(string name = "notes") =>
        new(name, "http", McpEntryScope.User, "~/.cursor/mcp.json", McpServerStatusKind.AuthRequired, null, null, "https://notes.example.com", [], [], true, true, false, false, true);
}

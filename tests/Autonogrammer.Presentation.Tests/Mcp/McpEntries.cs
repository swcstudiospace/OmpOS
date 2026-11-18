using Autonogrammer.Presentation.Mcp;

namespace Autonogrammer.Presentation.Tests.Mcp;

/// <summary>Builders for the server entries the MCP view model tests use. Each entry's consent identity is derived from its name, so a test changes it on purpose.</summary>
internal static class McpEntries
{
    /// <summary>The consent identity a builder gives the server of this name.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The identity.</returns>
    public static string IdentityOf(string name) => "identity:" + name;

    /// <summary>A user stdio server that is ready.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The entry.</returns>
    public static McpServerEntry Ready(string name = "files") =>
        new(name, "stdio", McpEntryScope.User, "~/.cursor/mcp.json", McpServerStatusKind.Configured, null, null, "npx -y files-mcp", "npx -y files-mcp", ["API_KEY"], [], true, true, false, false, false, IdentityOf(name));

    /// <summary>A project stdio server that waits for approval.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The entry.</returns>
    public static McpServerEntry NeedsConsent(string name = "tool") =>
        new(name, "stdio", McpEntryScope.Project, ".cursor/mcp.json", McpServerStatusKind.NeedsConsent, null, null, "node ./tool.js --serve", "node ./tool.js --serve", ["TOOL_TOKEN"], [], true, false, true, false, false, IdentityOf(name));

    /// <summary>A project remote server whose variable is not set.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The entry.</returns>
    public static McpServerEntry Failed(string name = "search") =>
        new(name, "http", McpEntryScope.Project, ".cursor/mcp.json", McpServerStatusKind.Failed, "Not set: environment variable SEARCH_TOKEN.", null, "https://mcp.example.com", "https://mcp.example.com", [], ["Authorization"], true, false, true, false, false, IdentityOf(name));

    /// <summary>A user remote server that asked for sign-in.</summary>
    /// <param name="name">The server name.</param>
    /// <returns>The entry.</returns>
    public static McpServerEntry AuthRequired(string name = "notes") =>
        new(name, "http", McpEntryScope.User, "~/.cursor/mcp.json", McpServerStatusKind.AuthRequired, null, null, "https://notes.example.com", "https://notes.example.com", [], [], true, true, false, false, true, IdentityOf(name));
}

namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>One tool call as the decision engine sees it. A field the agent did not send is null, and a call whose essential field is null asks.</summary>
/// <param name="Kind">The kind of call.</param>
/// <param name="ToolName">The tool's display name, for the audit record.</param>
/// <param name="Command">The shell command line, for <see cref="RunModeToolKind.Shell"/>.</param>
/// <param name="Paths">The files the call reads or writes, absolute or relative to <paramref name="WorkingDirectory"/>.</param>
/// <param name="FetchAddress">The address a fetch asks for, a full address or a bare host.</param>
/// <param name="McpServer">The MCP server's name, for <see cref="RunModeToolKind.Mcp"/>.</param>
/// <param name="McpTool">The MCP tool's name, for <see cref="RunModeToolKind.Mcp"/>.</param>
/// <param name="WorkingDirectory">The directory relative paths are resolved against; null means the workspace root.</param>
public sealed record RunModeRequest(
    RunModeToolKind Kind,
    string ToolName,
    string? Command = null,
    IReadOnlyList<string>? Paths = null,
    string? FetchAddress = null,
    string? McpServer = null,
    string? McpTool = null,
    string? WorkingDirectory = null)
{
    /// <summary>Creates a shell request.</summary>
    /// <param name="command">The command line, or null when it is not known.</param>
    /// <param name="workingDirectory">The directory the command runs in, or null for the workspace root.</param>
    /// <returns>The request.</returns>
    public static RunModeRequest ForShell(string? command, string? workingDirectory = null) =>
        new(RunModeToolKind.Shell, "shell", Command: command, WorkingDirectory: workingDirectory);

    /// <summary>Creates a read request.</summary>
    /// <param name="paths">The files read.</param>
    /// <returns>The request.</returns>
    public static RunModeRequest ForRead(params string[] paths) => new(RunModeToolKind.Read, "read", Paths: paths);

    /// <summary>Creates a write request.</summary>
    /// <param name="paths">The files written, deleted or moved.</param>
    /// <returns>The request.</returns>
    public static RunModeRequest ForWrite(params string[] paths) => new(RunModeToolKind.Write, "write", Paths: paths);

    /// <summary>Creates a fetch request.</summary>
    /// <param name="address">The address fetched.</param>
    /// <returns>The request.</returns>
    public static RunModeRequest ForFetch(string? address) => new(RunModeToolKind.Fetch, "fetch", FetchAddress: address);

    /// <summary>Creates an MCP request.</summary>
    /// <param name="server">The server's name.</param>
    /// <param name="tool">The tool's name.</param>
    /// <returns>The request.</returns>
    public static RunModeRequest ForMcp(string? server, string? tool) => new(RunModeToolKind.Mcp, "mcp", McpServer: server, McpTool: tool);
}

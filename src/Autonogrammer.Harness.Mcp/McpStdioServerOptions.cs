namespace Autonogrammer.Harness.Mcp;

/// <summary>How to launch a stdio MCP server. The command runs directly with an argument list; no shell is involved.</summary>
/// <param name="Command">The executable to run.</param>
/// <param name="Arguments">The arguments, passed one by one.</param>
/// <param name="WorkingDirectory">The explicit working directory for the server process.</param>
/// <param name="EnvironmentVariables">Variables to set for the server; <c>null</c> values unset a variable.</param>
public sealed record McpStdioServerOptions(
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null);

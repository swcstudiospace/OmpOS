using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Harness.Mcp;

/// <summary>How to launch a stdio MCP server. The command runs directly with an argument list; no shell is involved.</summary>
/// <param name="Command">The executable to run.</param>
/// <param name="Arguments">The arguments, passed one by one.</param>
/// <param name="WorkingDirectory">The explicit working directory for the server process.</param>
/// <param name="EnvironmentVariables">Variables to set for the server; <c>null</c> values unset a variable. They are applied after <paramref name="EnvironmentMode"/>, so they win.</param>
/// <param name="EnvironmentMode">
/// How the server obtains the rest of its environment. <see cref="EngineEnvironmentMode.Inherit"/> (the default) keeps whatever the SDK passes on;
/// <see cref="EngineEnvironmentMode.Allowlist"/> unsets every variable of this process that <see cref="EngineEnvironmentPolicy"/> does not allow.
/// </param>
/// <param name="EnvironmentAllowList">Extra variable names, or prefixes ending in <c>*</c>, kept in <see cref="EngineEnvironmentMode.Allowlist"/> mode. Null adds none.</param>
public sealed record McpStdioServerOptions(
    string Command,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?>? EnvironmentVariables = null,
    EngineEnvironmentMode EnvironmentMode = EngineEnvironmentMode.Inherit,
    IReadOnlyList<string>? EnvironmentAllowList = null);

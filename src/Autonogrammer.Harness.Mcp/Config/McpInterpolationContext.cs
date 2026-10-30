namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>The values a variable reference expands to while one file is read.</summary>
/// <param name="WorkspaceFolder">The folder <c>${workspaceFolder}</c> expands to, or <see langword="null"/> when there is none.</param>
/// <param name="UserHome">The folder <c>${userHome}</c> expands to, or <see langword="null"/> when there is none.</param>
/// <param name="Environment">The environment <c>${env:NAME}</c> reads.</param>
/// <param name="ClaudeStyle">Whether <c>${NAME}</c> and <c>${NAME:-default}</c> read the environment, as Claude Code's files do.</param>
internal sealed record McpInterpolationContext(
    string? WorkspaceFolder,
    string? UserHome,
    IReadOnlyDictionary<string, string?> Environment,
    bool ClaudeStyle);

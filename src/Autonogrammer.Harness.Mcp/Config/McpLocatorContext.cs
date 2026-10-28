namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>What the loader needs to find and expand configuration. Nothing here is read from the process: the head passes it in, so a test controls every value.</summary>
/// <param name="WorkspaceRoots">The open workspace roots in precedence order, in the file store's path form. The first is the primary root.</param>
/// <param name="UserHome">The user's home directory, or <see langword="null"/> to skip user files and leave <c>${userHome}</c> unresolved.</param>
/// <param name="EnvironmentVariables">The environment <c>${env:NAME}</c> reads. A name that is absent or <see langword="null"/> is unset.</param>
public sealed record McpLocatorContext(
    IReadOnlyList<string> WorkspaceRoots,
    string? UserHome,
    IReadOnlyDictionary<string, string?> EnvironmentVariables);

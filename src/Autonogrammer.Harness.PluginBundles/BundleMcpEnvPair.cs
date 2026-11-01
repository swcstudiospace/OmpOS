namespace Autonogrammer.Harness.PluginBundles;

/// <summary>One environment pair from a bundle MCP server. Values may be null.</summary>
/// <param name="Key">The variable name.</param>
/// <param name="Value">The variable value after root expansion.</param>
public sealed record BundleMcpEnvPair(string Key, string? Value);

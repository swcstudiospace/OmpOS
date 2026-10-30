namespace Autonogrammer.Harness.Mcp.Config;

/// <summary>Everything the loader found: the effective servers, the files they came from, and every problem.</summary>
/// <param name="Servers">The effective servers after precedence, highest-precedence file first, entries of a file in file order.</param>
/// <param name="Files">Every file that exists, in precedence order.</param>
/// <param name="Diagnostics">Every problem, including servers that precedence shadowed.</param>
public sealed record McpConfigSet(
    IReadOnlyList<McpServerSpec> Servers,
    IReadOnlyList<McpConfigFile> Files,
    IReadOnlyList<McpDiagnostic> Diagnostics)
{
    /// <summary>Gets an empty result.</summary>
    public static McpConfigSet Empty { get; } = new([], [], []);
}

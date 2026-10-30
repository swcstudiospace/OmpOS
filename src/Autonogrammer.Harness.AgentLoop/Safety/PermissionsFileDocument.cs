namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>What one <c>permissions.json</c> file says. A list is null when the file does not define its key, which is different from an empty list.</summary>
/// <param name="Location">The file.</param>
/// <param name="Exists">True when something is at the path.</param>
/// <param name="IsValid">True when the file was read; false when it was ignored (see the diagnostics).</param>
/// <param name="TerminalAllowlist">The <c>terminalAllowlist</c> entries, or null.</param>
/// <param name="McpAllowlist">The <c>mcpAllowlist</c> entries (each <c>server:tool</c>), or null.</param>
/// <param name="AllowInstructions">The <c>autoRun.allow_instructions</c> sentences.</param>
/// <param name="BlockInstructions">The <c>autoRun.block_instructions</c> sentences.</param>
/// <param name="AllowTokens">The tokens of <c>permissions.allow</c>.</param>
/// <param name="DenyTokens">The tokens of <c>permissions.deny</c>.</param>
/// <param name="Diagnostics">What was ignored and why.</param>
public sealed record PermissionsFileDocument(
    SafetyFileLocation Location,
    bool Exists,
    bool IsValid,
    IReadOnlyList<string>? TerminalAllowlist,
    IReadOnlyList<string>? McpAllowlist,
    IReadOnlyList<string> AllowInstructions,
    IReadOnlyList<string> BlockInstructions,
    IReadOnlyList<PermissionToken> AllowTokens,
    IReadOnlyList<PermissionToken> DenyTokens,
    IReadOnlyList<SafetyDiagnostic> Diagnostics);

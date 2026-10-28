namespace Autonogrammer.Presentation.Safety;

/// <summary>Everything the Run mode page shows, as the source read it.</summary>
public sealed record RunModeSnapshot
{
    /// <summary>Gets a snapshot of a user with no file and the defaults.</summary>
    public static RunModeSnapshot Empty { get; } = new() { Values = RunModeValues.Defaults };

    /// <summary>Gets the values the page edits.</summary>
    public required RunModeValues Values { get; init; }

    /// <summary>Gets the path of the Autonogrammer <c>permissions.json</c> the allowlists are saved to, or an empty string when there is none to write.</summary>
    public string EditPermissionsPath { get; init; } = string.Empty;

    /// <summary>Gets the path of the Autonogrammer <c>sandbox.json</c> the read boundary is saved to, or an empty string when there is none to write.</summary>
    public string EditSandboxPath { get; init; } = string.Empty;

    /// <summary>Gets the terminal allowlist entries of the files the page does not edit.</summary>
    public IReadOnlyList<RunModeEntry> OtherTerminalAllowlist { get; init; } = [];

    /// <summary>Gets the MCP allowlist entries of the files the page does not edit.</summary>
    public IReadOnlyList<RunModeEntry> OtherMcpAllowlist { get; init; } = [];

    /// <summary>Gets the read allowlist in force, with the file it comes from.</summary>
    public IReadOnlyList<RunModeEntry> EffectiveReadPaths { get; init; } = [];

    /// <summary>Gets every allow rule in force as a token, for example <c>Shell(git)</c>.</summary>
    public IReadOnlyList<RunModeEntry> AllowRules { get; init; } = [];

    /// <summary>Gets every deny rule in force as a token.</summary>
    public IReadOnlyList<RunModeEntry> DenyRules { get; init; } = [];

    /// <summary>Gets the <c>autoRun.allow_instructions</c> sentences. They are shown, never enforced.</summary>
    public IReadOnlyList<RunModeEntry> AllowInstructions { get; init; } = [];

    /// <summary>Gets the <c>autoRun.block_instructions</c> sentences. They are shown, never enforced.</summary>
    public IReadOnlyList<RunModeEntry> BlockInstructions { get; init; } = [];

    /// <summary>Gets the file that sets the read boundary in force, or null when the Settings value applies.</summary>
    public string? ReadBoundarySource { get; init; }

    /// <summary>Gets the file that sets the read allowlist in force, or null when the Settings value applies.</summary>
    public string? ReadPathsSource { get; init; }

    /// <summary>Gets every file that was looked at.</summary>
    public IReadOnlyList<RunModeFileRow> Files { get; init; } = [];

    /// <summary>Gets the problems found in the files.</summary>
    public IReadOnlyList<RunModeDiagnostic> Diagnostics { get; init; } = [];

    /// <summary>Gets a value indicating whether the workspace is trusted, so its allow entries apply.</summary>
    public bool ProjectTrusted { get; init; } = true;
}

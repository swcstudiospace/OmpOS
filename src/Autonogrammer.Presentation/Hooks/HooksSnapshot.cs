namespace Autonogrammer.Presentation.Hooks;

/// <summary>Everything the Hooks page shows, read in one step.</summary>
public sealed record HooksSnapshot
{
    /// <summary>Gets a value indicating whether the global Run hooks switch is on.</summary>
    public required bool RunHooks { get; init; }

    /// <summary>Gets the primary workspace root the hooks were read for, or an empty string when there is no workspace.</summary>
    public string WorkspaceRoot { get; init; } = string.Empty;

    /// <summary>Gets the hooks in run order, including disabled hooks.</summary>
    public required IReadOnlyList<HookItem> Hooks { get; init; }

    /// <summary>Gets the problems found while reading the hook files.</summary>
    public IReadOnlyList<HookDiagnosticItem> Diagnostics { get; init; } = [];

    /// <summary>Gets the hook files of the workspace that were not read because the workspace is not trusted.</summary>
    public IReadOnlyList<string> WithheldFiles { get; init; } = [];
}

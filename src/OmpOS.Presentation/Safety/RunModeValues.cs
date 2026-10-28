namespace OmpOS.Presentation.Safety;

/// <summary>
/// What the Run mode page edits: the mode, the read boundary, and the entries of the one OmpOS file each list is saved to. Entries of other files are shown and never edited.
/// Two values are equal when their lists hold the same entries in the same order.
/// </summary>
/// <param name="Mode">The run mode.</param>
/// <param name="ReadBoundary">The read boundary.</param>
/// <param name="TerminalAllowlist">The terminal commands that run without asking (the file's <c>terminalAllowlist</c>).</param>
/// <param name="McpAllowlist">The MCP tools that run without asking, each <c>server:tool</c> (the file's <c>mcpAllowlist</c>).</param>
/// <param name="AdditionalReadPaths">The read allowlist (the file's <c>additionalReadPaths</c>).</param>
public sealed record RunModeValues(
    RunModeChoice Mode,
    ReadBoundaryChoice ReadBoundary,
    IReadOnlyList<string> TerminalAllowlist,
    IReadOnlyList<string> McpAllowlist,
    IReadOnlyList<string> AdditionalReadPaths)
{
    /// <summary>Gets the defaults: Auto-review, read anywhere, no entries.</summary>
    public static RunModeValues Defaults { get; } = new(RunModeChoice.AutoReview, ReadBoundaryChoice.System, [], [], []);

    /// <inheritdoc/>
    public bool Equals(RunModeValues? other) =>
        other is not null
        && Mode == other.Mode
        && ReadBoundary == other.ReadBoundary
        && TerminalAllowlist.SequenceEqual(other.TerminalAllowlist, StringComparer.Ordinal)
        && McpAllowlist.SequenceEqual(other.McpAllowlist, StringComparer.Ordinal)
        && AdditionalReadPaths.SequenceEqual(other.AdditionalReadPaths, StringComparer.Ordinal);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Mode, ReadBoundary, TerminalAllowlist.Count, McpAllowlist.Count, AdditionalReadPaths.Count);
}

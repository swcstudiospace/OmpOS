namespace Autonogrammer.Contracts.WorkspaceSettings;

/// <summary>The answer of <c>GET /workspace/tree</c>: the entries of one folder. Secret files and links that leave the workspace are not listed.</summary>
public sealed record WorkspaceTreeResponse
{
    /// <summary>Gets the folder as a workspace-relative path with <c>/</c> separators; empty for the workspace folder itself.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the entries in ordinal name order.</summary>
    public required IReadOnlyList<WorkspaceTreeEntry> Entries { get; init; }

    /// <summary>Gets a value indicating whether the folder holds more entries than the listing returns.</summary>
    public required bool Truncated { get; init; }
}

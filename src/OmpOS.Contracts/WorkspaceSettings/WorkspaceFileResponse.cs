namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>The answer of <c>GET /workspace/file</c>: the text of one file.</summary>
public sealed record WorkspaceFileResponse
{
    /// <summary>Gets the file as a workspace-relative path with <c>/</c> separators.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the size of the file in bytes.</summary>
    public required long Size { get; init; }

    /// <summary>Gets the UTF-8 text of the file.</summary>
    public required string Content { get; init; }
}

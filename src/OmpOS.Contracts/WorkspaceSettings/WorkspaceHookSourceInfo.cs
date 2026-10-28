namespace OmpOS.Contracts.WorkspaceSettings;

/// <summary>One hook configuration file that was found.</summary>
public sealed record WorkspaceHookSourceInfo
{
    /// <summary>Gets the scope of the file.</summary>
    public required string Scope { get; init; }

    /// <summary>Gets the file, relative to the workspace folder or <c>~/</c> relative.</summary>
    public required string File { get; init; }

    /// <summary>Gets how the file is read.</summary>
    public required string Format { get; init; }

    /// <summary>Gets the position of the file in merge order; lower runs first.</summary>
    public required int Priority { get; init; }
}

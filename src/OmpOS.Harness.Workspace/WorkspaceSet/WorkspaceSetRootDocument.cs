namespace OmpOS.Workspace.Sets;

/// <summary>
/// One persisted root. The root at index 0 of its set is the primary.
/// </summary>
internal sealed class WorkspaceSetRootDocument
{
    public string Id { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? EnvironmentId { get; set; }
}

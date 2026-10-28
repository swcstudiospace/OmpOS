namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// One persisted set with the time it was last used.
/// </summary>
internal sealed class WorkspaceSetEntryDocument
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset LastUsedAt { get; set; }

    public List<WorkspaceSetRootDocument> Roots { get; set; } = [];
}

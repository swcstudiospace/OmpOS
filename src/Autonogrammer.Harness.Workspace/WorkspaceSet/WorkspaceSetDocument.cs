namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// The persisted recent sets, newest first. <see cref="Version"/> is bumped when the shape changes; a file with another version is
/// treated as empty.
/// </summary>
internal sealed class WorkspaceSetDocument
{
    public int Version { get; set; }

    public List<WorkspaceSetEntryDocument> Sets { get; set; } = [];
}

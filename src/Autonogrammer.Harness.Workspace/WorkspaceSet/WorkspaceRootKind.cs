namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// Where the directory of a <see cref="WorkspaceSetRoot"/> lives.
/// </summary>
public enum WorkspaceRootKind
{
    /// <summary>The directory is on this machine and can be probed and opened directly.</summary>
    Local,

    /// <summary>
    /// The directory is on another environment, named by <see cref="WorkspaceSetRoot.EnvironmentId"/> (an SSH or cloud environment profile).
    /// It cannot be probed from here; the environment decides whether it exists.
    /// </summary>
    Remote,
}

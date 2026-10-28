namespace Autonogrammer.Workspace.Sets;

/// <summary>
/// The questions <see cref="WorkspaceSetValidator"/> asks about local directories. They are a seam so the validator can be
/// tested without a file system and so a head can answer from its own sandbox rules.
/// </summary>
public interface IDirectoryProbe
{
    /// <summary>Returns whether the directory exists.</summary>
    /// <param name="path">An absolute local path.</param>
    bool Exists(string path);

    /// <summary>
    /// Returns the real path of an existing directory, with symbolic links resolved, or null when it does not exist. Two roots that
    /// reach one directory through different links have the same real path.
    /// </summary>
    /// <param name="path">An absolute local path.</param>
    string? RealPath(string path);

    /// <summary>Returns whether the directory is the top of a git working tree, that is, whether it directly contains a <c>.git</c> entry.</summary>
    /// <param name="path">An absolute local path.</param>
    bool IsGitRoot(string path);
}

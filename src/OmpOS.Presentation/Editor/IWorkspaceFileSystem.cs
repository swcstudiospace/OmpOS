namespace OmpOS.Presentation.Editor;

/// <summary>
/// The directory listing behind the workspace file tree (ADE-10). Paths are workspace-relative and normalised (see
/// <see cref="WorkspacePath"/>); the empty path names the root. An implementation never lists anything outside its root.
/// </summary>
public interface IWorkspaceFileSystem
{
    /// <summary>Gets the absolute path of the workspace root. Runs use it as their working directory.</summary>
    string RootPath { get; }

    /// <summary>Lists the immediate children of one directory.</summary>
    /// <param name="relativeDirectory">The normalised workspace-relative path of the directory. Empty for the root.</param>
    /// <returns>The children, in any order. Each entry's relative path is the directory's path joined with its name.</returns>
    /// <exception cref="DirectoryNotFoundException">No directory exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    IReadOnlyList<WorkspaceEntry> ListDirectory(string relativeDirectory);
}

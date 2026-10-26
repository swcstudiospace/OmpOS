namespace OmpOS.Presentation.Editor;

/// <summary>
/// The workspace files behind the file tree (ADE-10) and the diff review (ADE-11): directory listing, reading a text file and
/// replacing one. Paths are workspace-relative and normalised (see <see cref="WorkspacePath"/>); the empty path names the root.
/// An implementation never reaches anything outside its root. Reading and writing are optional: an implementation that does not
/// override them is read-only.
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

    /// <summary>Reads one text file as UTF-8, without its byte order mark.</summary>
    /// <param name="relativePath">The normalised workspace-relative path of the file.</param>
    /// <returns>The file's text.</returns>
    /// <exception cref="NotSupportedException">The implementation is read-only. This is the default.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    string ReadFile(string relativePath) => throw new NotSupportedException("This workspace file system is read-only.");

    /// <summary>
    /// Replaces the text of an existing file when it still holds the text the caller based the new text on. The write is atomic:
    /// the text goes to a temporary file in the same directory, which then replaces the file, so a reader sees the old or the new
    /// text and never a partial one. A byte order mark, and a Unix file mode, on the file are kept.
    /// </summary>
    /// <param name="relativePath">The normalised workspace-relative path of the file.</param>
    /// <param name="content">The new text, written as UTF-8.</param>
    /// <param name="expectedCurrent">The text the file must hold now. A file that holds anything else is left untouched.</param>
    /// <exception cref="NotSupportedException">The implementation is read-only. This is the default.</exception>
    /// <exception cref="StaleWorkspaceFileException">The file no longer holds <paramref name="expectedCurrent"/>.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    void WriteFile(string relativePath, string content, string expectedCurrent) => throw new NotSupportedException("This workspace file system is read-only.");
}

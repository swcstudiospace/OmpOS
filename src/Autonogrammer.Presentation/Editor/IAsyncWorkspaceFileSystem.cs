namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// The workspace files behind the file tree and the diff review when listing and reading may take time, as they do on a remote machine:
/// the same operations as <see cref="IWorkspaceFileSystem"/>, none of them blocking the caller. Paths are workspace-relative and
/// normalised (see <see cref="WorkspacePath"/>); the empty path names the root. An implementation never reaches anything outside its
/// root. Reading, writing, creating and deleting are optional: an implementation that does not override them is read-only.
/// </summary>
public interface IAsyncWorkspaceFileSystem
{
    /// <summary>Gets the absolute path of the workspace root, in the path form of the machine that holds the files.</summary>
    string RootPath { get; }

    /// <summary>Lists the immediate children of one directory.</summary>
    /// <param name="relativeDirectory">The normalised workspace-relative path of the directory. Empty for the root.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The children, in any order. Each entry's relative path is the directory's path joined with its name.</returns>
    /// <exception cref="DirectoryNotFoundException">No directory exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    /// <exception cref="IOException">The files could not be reached (for example the remote machine did not answer).</exception>
    Task<IReadOnlyList<WorkspaceEntry>> ListDirectoryAsync(string relativeDirectory, CancellationToken cancellationToken);

    /// <summary>Reads one text file as UTF-8, without its byte order mark.</summary>
    /// <param name="relativePath">The normalised workspace-relative path of the file.</param>
    /// <param name="maxBytes">The most bytes the file may hold. A larger file is not read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The file's text.</returns>
    /// <exception cref="NotSupportedException">The implementation is read-only. This is the default.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    /// <exception cref="WorkspaceFileTooLargeException">The file holds more than <paramref name="maxBytes"/> bytes.</exception>
    /// <exception cref="InvalidDataException">The file is binary or not valid UTF-8 text.</exception>
    Task<string> ReadFileAsync(string relativePath, int maxBytes, CancellationToken cancellationToken) =>
        Task.FromException<string>(new NotSupportedException("This workspace file system is read-only."));

    /// <summary>Describes one file or directory without reading it.</summary>
    /// <param name="relativePath">The normalised workspace-relative path.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>What is known about the path, or <see langword="null"/> when nothing exists there.</returns>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    Task<WorkspaceFileStat?> StatAsync(string relativePath, CancellationToken cancellationToken) =>
        Task.FromException<WorkspaceFileStat?>(new NotSupportedException("This workspace file system cannot describe a path."));

    /// <summary>
    /// Replaces the text of an existing file when it still holds the text the caller based the new text on. A reader sees the old or the new text
    /// and never a partial one. A byte order mark, and a Unix file mode, on the file are kept.
    /// </summary>
    /// <param name="relativePath">The normalised workspace-relative path of the file.</param>
    /// <param name="content">The new text, written as UTF-8.</param>
    /// <param name="expectedCurrent">The text the file must hold now. A file that holds anything else is left untouched.</param>
    /// <param name="cancellationToken">Cancels the write before it starts; a write that has started is finished or not applied.</param>
    /// <exception cref="NotSupportedException">The implementation is read-only. This is the default.</exception>
    /// <exception cref="StaleWorkspaceFileException">The file no longer holds <paramref name="expectedCurrent"/>.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    Task WriteFileAsync(string relativePath, string content, string expectedCurrent, CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException("This workspace file system is read-only."));

    /// <summary>Creates a new text file, and any missing parent directories, when no file exists at the path.</summary>
    /// <param name="relativePath">The normalised workspace-relative path of the new file.</param>
    /// <param name="content">The text of the file, written as UTF-8.</param>
    /// <param name="cancellationToken">Cancels the creation before it starts.</param>
    /// <exception cref="NotSupportedException">The implementation is read-only. This is the default.</exception>
    /// <exception cref="IOException">A file or directory already exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    Task CreateFileAsync(string relativePath, string content, CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException("This workspace file system is read-only."));

    /// <summary>Deletes a file when it still holds the text the caller based the deletion on.</summary>
    /// <param name="relativePath">The normalised workspace-relative path of the file.</param>
    /// <param name="expectedCurrent">The text the file must hold now.</param>
    /// <param name="cancellationToken">Cancels the deletion before it starts.</param>
    /// <exception cref="NotSupportedException">The implementation is read-only. This is the default.</exception>
    /// <exception cref="StaleWorkspaceFileException">The file no longer holds <paramref name="expectedCurrent"/>.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    Task DeleteFileAsync(string relativePath, string expectedCurrent, CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException("This workspace file system is read-only."));
}

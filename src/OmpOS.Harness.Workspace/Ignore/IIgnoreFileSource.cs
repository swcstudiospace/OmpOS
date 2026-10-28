namespace OmpOS.Workspace.Ignore;

/// <summary>
/// The file tree that ignore files are read from. The local disk is one source (<see cref="PhysicalIgnoreFileSource"/>); tests and the remote phases implement the same interface over
/// an in-memory or remote tree. Paths are relative to the workspace root and use <c>/</c> as the separator. No member throws for a missing or unreadable path: the result says what happened.
/// </summary>
public interface IIgnoreFileSource
{
    /// <summary>Reads one UTF-8 text file.</summary>
    /// <param name="relativePath">The file path relative to the workspace root.</param>
    /// <param name="maxBytes">
    /// The size cap. A file larger than the cap MUST NOT be read in full: the source returns at most <c>maxBytes + 1</c> bytes of it, and the caller treats text whose
    /// UTF-8 size exceeds <paramref name="maxBytes"/> as too large.
    /// </param>
    /// <returns>The text, or <see langword="null"/> when the file is missing or cannot be read.</returns>
    string? ReadText(string relativePath, long maxBytes);

    /// <summary>Asks whether the file, or any folder on its path, is a symbolic link. Ignore files behind links are never read.</summary>
    /// <param name="relativePath">The file path relative to the workspace root.</param>
    /// <returns><see langword="true"/> when the path goes through a symbolic link or cannot be read safely.</returns>
    bool IsSymbolicLink(string relativePath);
}

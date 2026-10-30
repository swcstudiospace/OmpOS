namespace Autonogrammer.Presentation.Editor;

/// <summary>
/// Writes a reviewed diff to disk (ADE-11). The file receives <see cref="DiffReview.Apply"/>: the accepted hunks applied to the
/// original, with rejected and pending hunks left as they were. The write is refused when the file on disk is no longer the
/// text the review was made against, so a newer file is never overwritten.
/// </summary>
public sealed class DiffReviewWriter
{
    private readonly IWorkspaceFileSystem _fileSystem;

    /// <summary>Creates a writer over a workspace file system.</summary>
    /// <param name="fileSystem">The workspace that holds the reviewed file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> is null.</exception>
    public DiffReviewWriter(IWorkspaceFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        _fileSystem = fileSystem;
    }

    /// <summary>Writes the review's result to a file.</summary>
    /// <param name="relativePath">The workspace-relative path of the file the diff was made against.</param>
    /// <param name="review">The reviewed diff. Its <see cref="DiffReview.Original"/> must still be the file's text.</param>
    /// <returns>
    /// <see cref="DiffWriteStatus.Written"/> when the file was replaced, <see cref="DiffWriteStatus.Unchanged"/> when no hunk is
    /// accepted and the file was left alone, <see cref="DiffWriteStatus.Stale"/> when the file changed on disk after the review began.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> or <paramref name="review"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> is not a valid workspace file path.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    public DiffWriteStatus Write(string relativePath, DiffReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        return Write(relativePath, review, review.Original);
    }

    /// <summary>
    /// Writes the review's result to a file that holds a different text than the review's original. A review of an agent's edit has the
    /// text before the edit as its original, so rejecting a hunk reverts it, while the file on disk holds the text after the edit.
    /// </summary>
    /// <param name="relativePath">The workspace-relative path of the file the diff was made against.</param>
    /// <param name="review">The reviewed diff.</param>
    /// <param name="expectedCurrent">The text the file must hold now. A file that holds anything else is left untouched.</param>
    /// <returns>
    /// <see cref="DiffWriteStatus.Written"/> when the file was replaced, <see cref="DiffWriteStatus.Unchanged"/> when the result already equals
    /// <paramref name="expectedCurrent"/> and the file was left alone, <see cref="DiffWriteStatus.Stale"/> when the file no longer holds it.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/>, <paramref name="review"/> or <paramref name="expectedCurrent"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> is not a valid workspace file path.</exception>
    /// <exception cref="FileNotFoundException">No file exists at the path.</exception>
    /// <exception cref="UnauthorizedAccessException">The path resolves outside the workspace root, through a link.</exception>
    public DiffWriteStatus Write(string relativePath, DiffReview review, string expectedCurrent)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(review);
        ArgumentNullException.ThrowIfNull(expectedCurrent);

        var path = WorkspacePath.Normalize(relativePath);
        var result = review.Apply();
        if (string.Equals(result, expectedCurrent, StringComparison.Ordinal))
        {
            // The file already holds the result: a stale file does not matter, because nothing is written.
            return DiffWriteStatus.Unchanged;
        }

        try
        {
            _fileSystem.WriteFile(path, result, expectedCurrent);
        }
        catch (StaleWorkspaceFileException)
        {
            return DiffWriteStatus.Stale;
        }

        return DiffWriteStatus.Written;
    }
}

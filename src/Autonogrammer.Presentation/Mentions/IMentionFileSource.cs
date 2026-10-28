namespace Autonogrammer.Presentation.Mentions;

/// <summary>
/// The workspace tree and file reads the file and folder mentions need. It is a seam so the mention code runs over a local directory, a remote
/// machine or a test fake; the implementation owns caching, the cap on the tree size and the safety of paths (nothing outside the root).
/// </summary>
public interface IMentionFileSource
{
    /// <summary>Lists the workspace tree: files and folders, without the contents of folders the implementation skips (for example <c>.git</c>).</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The entries in any order.</returns>
    Task<IReadOnlyList<MentionFileEntry>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Reads a text file.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <param name="maxBytes">The byte cap; a longer file is cut on a character boundary.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text and the full size.</returns>
    /// <exception cref="MentionUnavailableException">The file is binary, missing, or outside the workspace.</exception>
    Task<MentionContent> ReadAsync(string relativePath, int maxBytes, CancellationToken cancellationToken);

    /// <summary>Builds the resource identifier of a workspace path.</summary>
    /// <param name="relativePath">The workspace-relative path.</param>
    /// <returns>An absolute <c>file:</c> URI.</returns>
    Uri ToUri(string relativePath);
}

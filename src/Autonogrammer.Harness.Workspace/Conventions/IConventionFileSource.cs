namespace Autonogrammer.Workspace.Conventions;

/// <summary>
/// The file tree that convention discovery reads. A local directory is one source (<see cref="LocalConventionFileSource"/>); the SSH and cloud
/// phases implement the same interface over a remote or a cloud file tree. Paths are absolute in the source's own namespace and use <c>/</c> as the
/// separator in every source (Windows accepts it too). No member throws for a missing or unreadable path: the result says what happened.
/// </summary>
public interface IConventionFileSource
{
    /// <summary>Lists one folder.</summary>
    /// <param name="directory">The folder path.</param>
    /// <param name="maxEntries">The most entries to return; a larger folder is cut at this count.</param>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The entries in ordinal name order, an empty list for a folder that does not exist, or a problem.</returns>
    Task<ConventionListResult> ListAsync(string directory, int maxEntries, CancellationToken cancellationToken);

    /// <summary>Reads one UTF-8 text file.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="maxBytes">The size cap. A larger file is not read and the result is <see cref="ConventionDiagnosticKind.TooLarge"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text or the problem.</returns>
    Task<ConventionReadResult> ReadTextAsync(string path, int maxBytes, CancellationToken cancellationToken);

    /// <summary>Asks whether a file or a folder exists.</summary>
    /// <param name="path">The path.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns><see langword="true"/> when the path exists and may be read.</returns>
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken);
}

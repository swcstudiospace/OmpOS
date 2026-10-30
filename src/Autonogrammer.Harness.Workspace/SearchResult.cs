namespace Autonogrammer.Workspace;

/// <summary>
/// The result of a search. An invalid or unusable request is an error result, not an exception.
/// </summary>
/// <param name="Hits">The matches, ordered by file path, then line, then position in the line.</param>
/// <param name="Truncated">Whether more hits existed than the request allowed.</param>
/// <param name="Error">Why the search failed, or null when it succeeded.</param>
public sealed record SearchResult(IReadOnlyList<SearchHit> Hits, bool Truncated, string? Error)
{
    /// <summary>
    /// Gets whether the search failed.
    /// </summary>
    public bool IsError => Error is not null;

    /// <summary>
    /// Creates an error result with no hits.
    /// </summary>
    /// <param name="error">Why the search failed.</param>
    public static SearchResult Failure(string error) => new([], Truncated: false, error);
}

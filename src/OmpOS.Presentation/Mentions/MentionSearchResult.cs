namespace OmpOS.Presentation.Mentions;

/// <summary>What one source found for a query.</summary>
/// <param name="Items">The rows, best first, at most the limit the source was given.</param>
/// <param name="UnavailableReason">Why the source cannot answer at all (for example a pull request host is not configured), or <see langword="null"/>.</param>
/// <param name="HasMore">Whether the source cut its list at the limit.</param>
public sealed record MentionSearchResult(IReadOnlyList<MentionCandidate> Items, string? UnavailableReason = null, bool HasMore = false)
{
    /// <summary>Gets a result with no rows and no reason.</summary>
    public static MentionSearchResult Empty { get; } = new([]);

    /// <summary>Builds the result of a source that cannot answer.</summary>
    /// <param name="reason">Why, as a plain sentence.</param>
    /// <returns>A result with no rows and the reason.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reason"/> is null.</exception>
    public static MentionSearchResult Unavailable(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return new MentionSearchResult([], reason);
    }
}

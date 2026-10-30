namespace Autonogrammer.Presentation.Mentions;

/// <summary>The pull requests a <c>#</c> mention can attach. Without a configured host the source is unavailable and says why.</summary>
public interface IPullRequestSource
{
    /// <summary>Gets whether a host is configured and reachable in principle.</summary>
    bool IsAvailable { get; }

    /// <summary>Gets why the source is unavailable, or <see langword="null"/> when <see cref="IsAvailable"/>.</summary>
    string? UnavailableReason { get; }

    /// <summary>Searches pull requests by number or title.</summary>
    /// <param name="query">The text after the <c>#</c>; empty lists the most recent.</param>
    /// <param name="limit">The most rows to return.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The pull requests, best first.</returns>
    Task<IReadOnlyList<MentionPullRequest>> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    /// <summary>Reads a pull request (title, description and diff) as text.</summary>
    /// <param name="number">The pull request number.</param>
    /// <param name="maxBytes">The byte cap.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text.</returns>
    /// <exception cref="MentionUnavailableException">The pull request cannot be read.</exception>
    Task<MentionContent> ReadAsync(int number, int maxBytes, CancellationToken cancellationToken);
}

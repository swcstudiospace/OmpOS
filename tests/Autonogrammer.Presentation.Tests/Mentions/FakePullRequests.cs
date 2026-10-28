using Autonogrammer.Presentation.Mentions;

namespace Autonogrammer.Presentation.Tests.Mentions;

/// <summary>A configured pull request host with a fixed list.</summary>
internal sealed class FakePullRequests : IPullRequestSource
{
    /// <summary>Gets the pull requests the host knows.</summary>
    internal List<MentionPullRequest> Pulls { get; } = [];

    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public string? UnavailableReason => null;

    /// <inheritdoc />
    public Task<IReadOnlyList<MentionPullRequest>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        IReadOnlyList<MentionPullRequest> found = Pulls
            .Where(pull => query.Length == 0
                || pull.Number.ToString(System.Globalization.CultureInfo.InvariantCulture).StartsWith(query, StringComparison.Ordinal)
                || pull.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
        return Task.FromResult(found);
    }

    /// <inheritdoc />
    public Task<MentionContent> ReadAsync(int number, int maxBytes, CancellationToken cancellationToken) =>
        Task.FromResult(new MentionContent("PR " + number, 4, false));
}

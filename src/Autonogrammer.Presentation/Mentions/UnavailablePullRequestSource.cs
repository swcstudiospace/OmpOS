namespace Autonogrammer.Presentation.Mentions;

/// <summary>The pull request source of a workspace with no configured host: unavailable, with a reason that names the fix.</summary>
public sealed class UnavailablePullRequestSource : IPullRequestSource
{
    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public string? UnavailableReason => MentionCopy.PullRequestsNoHost;

    /// <inheritdoc/>
    public Task<IReadOnlyList<MentionPullRequest>> SearchAsync(string query, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MentionPullRequest>>([]);

    /// <inheritdoc/>
    public Task<MentionContent> ReadAsync(int number, int maxBytes, CancellationToken cancellationToken) =>
        Task.FromException<MentionContent>(new MentionUnavailableException(MentionCopy.PullRequestsNoHost));
}

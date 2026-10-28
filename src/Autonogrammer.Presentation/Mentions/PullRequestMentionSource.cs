using System.Globalization;

namespace Autonogrammer.Presentation.Mentions;

/// <summary>The pull request mention: <c>#123</c> or <c>#title words</c>. Without a configured host the source says so instead of listing anything.</summary>
public sealed class PullRequestMentionSource : IMentionSource
{
    private readonly IPullRequestSource _pullRequests;

    /// <summary>Creates the source.</summary>
    /// <param name="pullRequests">The pull request host, for example <see cref="UnavailablePullRequestSource"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pullRequests"/> is null.</exception>
    public PullRequestMentionSource(IPullRequestSource pullRequests)
    {
        ArgumentNullException.ThrowIfNull(pullRequests);

        _pullRequests = pullRequests;
    }

    /// <inheritdoc/>
    public MentionKind Kind => MentionKind.PullRequest;

    /// <inheritdoc/>
    public MentionTrigger Trigger => MentionTrigger.Hash;

    /// <inheritdoc/>
    public async Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_pullRequests.IsAvailable)
        {
            return MentionSearchResult.Unavailable(_pullRequests.UnavailableReason ?? MentionCopy.PullRequestsNoHost);
        }

        var found = await _pullRequests.SearchAsync(query, limit + 1, cancellationToken).ConfigureAwait(false);
        var items = found
            .Take(limit)
            .Select(static pull => new MentionCandidate(
                MentionKind.PullRequest,
                pull.Number.ToString(CultureInfo.InvariantCulture),
                pull.Number.ToString(CultureInfo.InvariantCulture),
                pull.Title + " (" + pull.State + ")"))
            .ToList();
        return new MentionSearchResult(items, null, found.Count > limit);
    }

    /// <inheritdoc/>
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (!_pullRequests.IsAvailable)
        {
            return Task.FromException<ResolvedMention>(new MentionUnavailableException(_pullRequests.UnavailableReason ?? MentionCopy.PullRequestsNoHost));
        }

        if (!int.TryParse(candidate.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            return Task.FromException<ResolvedMention>(new MentionUnavailableException(MentionCopy.CannotAttach));
        }

        var mention = new ResolvedMention(
            MentionKind.PullRequest,
            candidate.Id,
            candidate.Display,
            MentionUris.Custom("pr", candidate.Id),
            "text/markdown",
            -1,
            false,
            (maxBytes, token) => _pullRequests.ReadAsync(number, maxBytes, token));
        return Task.FromResult(mention);
    }
}

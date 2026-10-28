using System.Globalization;

namespace Autonogrammer.Presentation.Mentions;

/// <summary>
/// The git diff mentions: <c>@Commit</c> (the diff of the working state) and <c>@Branch</c> (the diff with the base branch). One instance serves one kind. The
/// row is listed disabled, with the reason, when the workspace has no repository or no base branch.
/// </summary>
public sealed class GitDiffMentionSource : IMentionSource
{
    private readonly IGitDiffSource _git;
    private readonly MentionKind _kind;

    /// <summary>Creates the source.</summary>
    /// <param name="kind"><see cref="MentionKind.GitDiffWorking"/> or <see cref="MentionKind.GitDiffBranch"/>.</param>
    /// <param name="git">The git client.</param>
    /// <exception cref="ArgumentNullException"><paramref name="git"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is neither git kind.</exception>
    public GitDiffMentionSource(MentionKind kind, IGitDiffSource git)
    {
        ArgumentNullException.ThrowIfNull(git);
        if (kind is not (MentionKind.GitDiffWorking or MentionKind.GitDiffBranch))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "A git source serves GitDiffWorking or GitDiffBranch.");
        }

        _kind = kind;
        _git = git;
    }

    /// <inheritdoc/>
    public MentionKind Kind => _kind;

    /// <inheritdoc/>
    public MentionTrigger Trigger => MentionTrigger.At;

    private MentionGitDiffKind DiffKind => _kind == MentionKind.GitDiffWorking ? MentionGitDiffKind.Working : MentionGitDiffKind.Branch;

    private string Display => _kind == MentionKind.GitDiffWorking ? MentionCopy.GitWorkingDisplay : MentionCopy.GitBranchDisplay;

    /// <inheritdoc/>
    public async Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        string[] aliases = _kind == MentionKind.GitDiffWorking
            ? [Display, "working", "diff", "changes", "uncommitted", "git"]
            : [Display, "main", "diff", "base", "git"];
        var score = query.Length == 0 ? 0 : aliases.Max(alias => MentionFuzzy.Score(query, alias));
        if (score < 0 || limit <= 0)
        {
            return MentionSearchResult.Empty;
        }

        var summary = await _git.GetSummaryAsync(DiffKind, cancellationToken).ConfigureAwait(false);
        var detail = summary.Available
            ? DescribeAvailable(summary)
            : null;
        var reason = summary.Available ? null : summary.Reason ?? MentionCopy.NoRepository;
        return new MentionSearchResult([new MentionCandidate(_kind, Display, Display, detail, score, DisabledReason: reason)]);
    }

    /// <inheritdoc/>
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.DisabledReason is not null)
        {
            return Task.FromException<ResolvedMention>(new MentionUnavailableException(candidate.DisabledReason));
        }

        var kind = DiffKind;
        var mention = new ResolvedMention(
            _kind,
            candidate.Id,
            candidate.Display,
            MentionUris.Custom("git", kind == MentionGitDiffKind.Working ? "working" : "branch"),
            "text/x-diff",
            -1,
            false,
            async (maxBytes, token) => await _git.ReadAsync(kind, maxBytes, token).ConfigureAwait(false));
        return Task.FromResult(mention);
    }

    private string DescribeAvailable(MentionGitDiffSummary summary)
    {
        var files = summary.FileCount == 1 ? "1 file" : string.Create(CultureInfo.InvariantCulture, $"{summary.FileCount} files");
        if (_kind == MentionKind.GitDiffWorking)
        {
            return MentionCopy.GitWorkingDetail + ", " + files;
        }

        return "Diff with " + (summary.BaseRef ?? "main") + ", " + files;
    }
}

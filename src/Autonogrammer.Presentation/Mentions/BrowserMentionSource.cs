namespace Autonogrammer.Presentation.Mentions;

/// <summary>The browser mention: <c>@Browser</c> attaches the text of the page the built-in browser shows. It is listed disabled while no page is open.</summary>
public sealed class BrowserMentionSource : IMentionSource
{
    private readonly IMentionBrowserProvider _browser;

    /// <summary>Creates the source.</summary>
    /// <param name="browser">The browser.</param>
    /// <exception cref="ArgumentNullException"><paramref name="browser"/> is null.</exception>
    public BrowserMentionSource(IMentionBrowserProvider browser)
    {
        ArgumentNullException.ThrowIfNull(browser);

        _browser = browser;
    }

    /// <inheritdoc/>
    public MentionKind Kind => MentionKind.Browser;

    /// <inheritdoc/>
    public MentionTrigger Trigger => MentionTrigger.At;

    /// <inheritdoc/>
    public Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var title = _browser.CurrentTitle;
        var score = query.Length == 0 ? 0 : Math.Max(MentionFuzzy.Score(query, MentionCopy.BrowserDisplay), title is null ? -1 : MentionFuzzy.Score(query, title));
        if (score < 0 || limit <= 0)
        {
            return Task.FromResult(MentionSearchResult.Empty);
        }

        var candidate = new MentionCandidate(
            MentionKind.Browser,
            MentionCopy.BrowserDisplay,
            MentionCopy.BrowserDisplay,
            title,
            score,
            DisabledReason: title is null ? MentionCopy.BrowserNoPage : null);
        return Task.FromResult(new MentionSearchResult([candidate]));
    }

    /// <inheritdoc/>
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.DisabledReason is not null)
        {
            return Task.FromException<ResolvedMention>(new MentionUnavailableException(candidate.DisabledReason));
        }

        var mention = new ResolvedMention(
            MentionKind.Browser,
            candidate.Id,
            candidate.Display,
            MentionUris.Custom("browser", "page"),
            "text/plain",
            -1,
            false,
            async (maxBytes, token) =>
            {
                var page = await _browser.CaptureAsync(maxBytes, token).ConfigureAwait(false);
                var cut = MentionText.Cut(page.Title + "\n" + page.Url.AbsoluteUri + "\n\n" + page.Text, maxBytes, keepEnd: false);
                return cut with { SizeBytes = Math.Max(cut.SizeBytes, page.SizeBytes) };
            });
        return Task.FromResult(mention);
    }
}

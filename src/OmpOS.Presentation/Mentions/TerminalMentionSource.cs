namespace OmpOS.Presentation.Mentions;

/// <summary>The terminal mention: <c>@terminal</c> attaches the recent output of a terminal session.</summary>
public sealed class TerminalMentionSource : IMentionSource
{
    private readonly IMentionTerminalProvider _terminals;

    /// <summary>Creates the source.</summary>
    /// <param name="terminals">The terminals.</param>
    /// <exception cref="ArgumentNullException"><paramref name="terminals"/> is null.</exception>
    public TerminalMentionSource(IMentionTerminalProvider terminals)
    {
        ArgumentNullException.ThrowIfNull(terminals);

        _terminals = terminals;
    }

    /// <inheritdoc/>
    public MentionKind Kind => MentionKind.Terminal;

    /// <inheritdoc/>
    public MentionTrigger Trigger => MentionTrigger.At;

    /// <inheritdoc/>
    public async Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var terminals = await _terminals.ListAsync(cancellationToken).ConfigureAwait(false);
        var ranked = MentionFuzzy.Rank(terminals, query, static terminal => terminal.Title, limit + 1);
        var items = ranked
            .Take(limit)
            .Select(static pair => new MentionCandidate(MentionKind.Terminal, pair.Item.Id, pair.Item.Title, pair.Item.IsRunning ? "running" : "finished", pair.Score))
            .ToList();
        return new MentionSearchResult(items, null, ranked.Count > limit);
    }

    /// <inheritdoc/>
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var id = candidate.Id;
        var mention = new ResolvedMention(
            MentionKind.Terminal,
            id,
            candidate.Display,
            MentionUris.Custom("terminal", id),
            "text/plain",
            -1,
            false,
            (maxBytes, token) => _terminals.ReadOutputAsync(id, maxBytes, token));
        return Task.FromResult(mention);
    }
}

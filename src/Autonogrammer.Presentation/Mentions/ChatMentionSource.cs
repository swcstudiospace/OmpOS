namespace Autonogrammer.Presentation.Mentions;

/// <summary>
/// The chat mention (<c>@chat</c>) and the side chat mention (<c>@side-chat</c>): attaches another conversation's transcript. One instance serves one kind;
/// it lists only the chats of that kind, most recently active first while the query is empty.
/// </summary>
public sealed class ChatMentionSource : IMentionSource
{
    private readonly IMentionChatProvider _chats;
    private readonly MentionKind _kind;

    /// <summary>Creates the source.</summary>
    /// <param name="kind"><see cref="MentionKind.Chat"/> or <see cref="MentionKind.SideChat"/>.</param>
    /// <param name="chats">The chats.</param>
    /// <exception cref="ArgumentNullException"><paramref name="chats"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is neither chat kind.</exception>
    public ChatMentionSource(MentionKind kind, IMentionChatProvider chats)
    {
        ArgumentNullException.ThrowIfNull(chats);
        if (kind is not (MentionKind.Chat or MentionKind.SideChat))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "A chat source serves Chat or SideChat.");
        }

        _kind = kind;
        _chats = chats;
    }

    /// <inheritdoc/>
    public MentionKind Kind => _kind;

    /// <inheritdoc/>
    public MentionTrigger Trigger => MentionTrigger.At;

    /// <inheritdoc/>
    public async Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var all = await _chats.ListAsync(cancellationToken).ConfigureAwait(false);
        var wantSide = _kind == MentionKind.SideChat;
        var chats = all
            .Where(chat => chat.IsSideChat == wantSide)
            .OrderByDescending(static chat => chat.LastActivity)
            .ThenBy(static chat => chat.Title, StringComparer.Ordinal);
        var ranked = MentionFuzzy.Rank(chats, query, static chat => chat.Title, limit + 1);
        var kind = _kind;
        var items = ranked
            .Take(limit)
            .Select(pair => new MentionCandidate(kind, pair.Item.Id, pair.Item.Title, null, pair.Score))
            .ToList();
        return new MentionSearchResult(items, null, ranked.Count > limit);
    }

    /// <inheritdoc/>
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var id = candidate.Id;
        var mention = new ResolvedMention(
            _kind,
            id,
            candidate.Display,
            MentionUris.Custom("chat", id),
            "text/plain",
            -1,
            false,
            (maxBytes, token) => _chats.ReadTranscriptAsync(id, maxBytes, token));
        return Task.FromResult(mention);
    }
}

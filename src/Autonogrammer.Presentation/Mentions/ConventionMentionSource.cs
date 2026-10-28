namespace Autonogrammer.Presentation.Mentions;

/// <summary>The rule mention (<c>@rule-name</c>) and the skill mention (<c>@skill-name</c>). One instance serves one kind.</summary>
public sealed class ConventionMentionSource : IMentionSource
{
    private readonly IMentionConventionProvider _conventions;
    private readonly MentionKind _kind;

    /// <summary>Creates the source.</summary>
    /// <param name="kind"><see cref="MentionKind.Rule"/> or <see cref="MentionKind.Skill"/>.</param>
    /// <param name="conventions">The rules and skills.</param>
    /// <exception cref="ArgumentNullException"><paramref name="conventions"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is neither Rule nor Skill.</exception>
    public ConventionMentionSource(MentionKind kind, IMentionConventionProvider conventions)
    {
        ArgumentNullException.ThrowIfNull(conventions);
        if (kind is not (MentionKind.Rule or MentionKind.Skill))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "A convention source serves Rule or Skill.");
        }

        _kind = kind;
        _conventions = conventions;
    }

    /// <inheritdoc/>
    public MentionKind Kind => _kind;

    /// <inheritdoc/>
    public MentionTrigger Trigger => MentionTrigger.At;

    /// <inheritdoc/>
    public async Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var items = _kind == MentionKind.Rule
            ? await _conventions.ListRulesAsync(cancellationToken).ConfigureAwait(false)
            : await _conventions.ListSkillsAsync(cancellationToken).ConfigureAwait(false);
        var ranked = MentionFuzzy.Rank(items.OrderBy(static item => item.Name, StringComparer.Ordinal), query, static item => item.Name, limit + 1);
        var kind = _kind;
        var rows = ranked
            .Take(limit)
            .Select(pair => new MentionCandidate(kind, pair.Item.Name, pair.Item.Name, Describe(pair.Item), pair.Score))
            .ToList();
        return new MentionSearchResult(rows, null, ranked.Count > limit);
    }

    /// <inheritdoc/>
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var name = candidate.Id;
        var kind = _kind;
        var mention = new ResolvedMention(
            kind,
            name,
            candidate.Display,
            MentionUris.Custom(kind == MentionKind.Rule ? "rule" : "skill", name),
            "text/markdown",
            -1,
            false,
            (maxBytes, token) => _conventions.ReadAsync(kind, name, maxBytes, token));
        return Task.FromResult(mention);
    }

    private static string? Describe(MentionConventionItem item) => (item.Description, item.Scope) switch
    {
        ({ Length: > 0 } description, { Length: > 0 } scope) => description + " (" + scope + ")",
        ({ Length: > 0 } description, _) => description,
        (_, { Length: > 0 } scope) => scope,
        _ => null,
    };
}

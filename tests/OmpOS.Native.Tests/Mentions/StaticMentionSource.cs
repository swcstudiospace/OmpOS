using OmpOS.Presentation.Mentions;

namespace OmpOS.Native.Tests.Mentions;

/// <summary>A mention source over a fixed list of names: a name matches when it holds the query, and a resolved mention reads a fixed body.</summary>
internal sealed class StaticMentionSource : IMentionSource
{
    private readonly string[] _names;

    /// <summary>Creates the source.</summary>
    /// <param name="kind">What the source attaches.</param>
    /// <param name="names">The names it lists.</param>
    internal StaticMentionSource(MentionKind kind, params string[] names)
    {
        Kind = kind;
        _names = names;
    }

    /// <inheritdoc />
    public MentionKind Kind { get; }

    /// <inheritdoc />
    public MentionTrigger Trigger => MentionTrigger.At;

    /// <inheritdoc />
    public Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var items = _names
            .Where(name => query.Length == 0 || name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Select(name => new MentionCandidate(Kind, name, name, "in the workspace", 0, SizeBytes: 10))
            .ToList();
        return Task.FromResult(new MentionSearchResult(items));
    }

    /// <inheritdoc />
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken) =>
        Task.FromResult(new ResolvedMention(
            Kind,
            candidate.Id,
            candidate.Display,
            new Uri("file:///w/" + candidate.Id),
            "text/plain",
            10,
            false,
            (_, _) => Task.FromResult(new MentionContent("body", 4, false))));
}

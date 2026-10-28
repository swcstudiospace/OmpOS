using OmpOS.Presentation.Mentions;

namespace OmpOS.Presentation.Tests.Mentions;

/// <summary>A mention source whose search is a delegate the test supplies, so a test controls what comes back, when, and how it fails.</summary>
internal sealed class ScriptedMentionSource : IMentionSource
{
    private readonly Func<string, int, CancellationToken, Task<MentionSearchResult>> _search;

    /// <summary>Creates a source.</summary>
    /// <param name="kind">What the source attaches.</param>
    /// <param name="search">Answers a search.</param>
    /// <param name="trigger">The trigger the source serves.</param>
    internal ScriptedMentionSource(MentionKind kind, Func<string, int, CancellationToken, Task<MentionSearchResult>> search, MentionTrigger trigger = MentionTrigger.At)
    {
        Kind = kind;
        Trigger = trigger;
        _search = search;
    }

    /// <inheritdoc />
    public MentionKind Kind { get; }

    /// <inheritdoc />
    public MentionTrigger Trigger { get; }

    /// <summary>Gets the queries the source was asked, in order.</summary>
    internal List<string> Queries { get; } = [];

    /// <summary>Gets the limits the source was given, in order.</summary>
    internal List<int> Limits { get; } = [];

    /// <summary>Builds a source that answers the given rows at once.</summary>
    /// <param name="kind">What the source attaches.</param>
    /// <param name="candidates">The rows.</param>
    /// <returns>The source.</returns>
    internal static ScriptedMentionSource Returning(MentionKind kind, params MentionCandidate[] candidates) =>
        new(kind, (_, _, _) => Task.FromResult(new MentionSearchResult(candidates)));

    /// <inheritdoc />
    public Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        Queries.Add(query);
        Limits.Add(limit);
        return _search(query, limit, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken) =>
        Task.FromResult(new ResolvedMention(
            Kind,
            candidate.Id,
            candidate.Display,
            new Uri("ompos://scripted/" + Uri.EscapeDataString(candidate.Id)),
            "text/plain",
            candidate.SizeBytes,
            false,
            (_, _) => Task.FromResult(new MentionContent("body of " + candidate.Id, 10, false))));
}

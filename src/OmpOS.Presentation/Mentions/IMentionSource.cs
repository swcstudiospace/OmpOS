namespace OmpOS.Presentation.Mentions;

/// <summary>One kind of mention: how to search it and how to attach what was picked.</summary>
public interface IMentionSource
{
    /// <summary>Gets what the source attaches.</summary>
    MentionKind Kind { get; }

    /// <summary>Gets the trigger that opens this source: <c>@</c> or <c>#</c>.</summary>
    MentionTrigger Trigger { get; }

    /// <summary>Searches the source. An empty query lists the first rows (the most useful ones) of the source.</summary>
    /// <param name="query">The name typed after the trigger.</param>
    /// <param name="limit">The most rows to return.</param>
    /// <param name="cancellationToken">Cancels the search when the user types on.</param>
    /// <returns>The rows, best first, or the reason the source is unavailable.</returns>
    Task<MentionSearchResult> SearchAsync(string query, int limit, CancellationToken cancellationToken);

    /// <summary>Turns a picked row into a mention that can read its content at send time.</summary>
    /// <param name="candidate">A row this source returned.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The mention.</returns>
    /// <exception cref="MentionUnavailableException">The row cannot be attached.</exception>
    Task<ResolvedMention> ResolveAsync(MentionCandidate candidate, CancellationToken cancellationToken);
}

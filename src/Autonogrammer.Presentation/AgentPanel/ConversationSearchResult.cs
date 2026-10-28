namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>One conversation that matches a search.</summary>
/// <param name="SessionId">The session to open.</param>
/// <param name="Title">The session title.</param>
/// <param name="Snippet">The text around the first match in the transcript, on one line; empty for a match in the title only.</param>
/// <param name="MatchStart">Where the match starts inside <paramref name="Snippet"/>, or -1 when there is none.</param>
/// <param name="MatchLength">The length of the match in <paramref name="Snippet"/>.</param>
/// <param name="IsTitleMatch">Whether every word of the query is in the title.</param>
/// <param name="Score">The rank; higher is better.</param>
public sealed record ConversationSearchResult(string SessionId, string Title, string Snippet, int MatchStart, int MatchLength, bool IsTitleMatch, double Score)
{
    /// <summary>Gets whether the result has a snippet.</summary>
    public bool HasSnippet => Snippet.Length > 0;

    /// <summary>Gets the name a screen reader speaks: the title and the snippet.</summary>
    public string AutomationName => HasSnippet ? Title + ". " + Snippet : Title;
}

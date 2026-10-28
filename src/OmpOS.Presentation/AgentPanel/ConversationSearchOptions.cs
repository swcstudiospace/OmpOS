namespace OmpOS.Presentation.AgentPanel;

/// <summary>The bounds of the conversation search index. Every limit keeps the index small enough to search on each keystroke.</summary>
public sealed record ConversationSearchOptions
{
    /// <summary>Gets the most sessions the index holds. The least recently updated session is dropped past it.</summary>
    public int MaxSessions { get; init; } = 200;

    /// <summary>Gets the most characters of transcript text kept per session. The end of the transcript is kept, because it is the most recent.</summary>
    public int MaxCharsPerSession { get; init; } = 200_000;

    /// <summary>Gets the most characters of transcript text kept over all sessions. The text of the least recently updated sessions is dropped first.</summary>
    public int MaxTotalChars { get; init; } = 4_000_000;

    /// <summary>Gets the number of characters shown on each side of a match in a snippet.</summary>
    public int SnippetRadius { get; init; } = 48;

    /// <summary>Gets the most words of a query that are used. Further words are ignored.</summary>
    public int MaxQueryTerms { get; init; } = 8;

    /// <summary>Gets the most characters of a query that are used.</summary>
    public int MaxQueryChars { get; init; } = 200;
}

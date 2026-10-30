namespace Autonogrammer.Presentation.Mentions;

/// <summary>
/// One mention found in the composer text: the trigger, the name typed after it, and where the token sits. The span covers the trigger and,
/// for a quoted name, both quotes.
/// </summary>
/// <param name="Trigger">The character that opened the mention.</param>
/// <param name="Query">The name after the trigger. Quotes are removed; it may be empty while the user has typed only the trigger.</param>
/// <param name="Start">The index of the trigger in the text.</param>
/// <param name="Length">The length of the token, trigger and quotes included.</param>
/// <param name="IsQuoted">Whether the name was written in double quotes, so that it may hold spaces.</param>
/// <param name="IsClosed">For a quoted name, whether the closing quote was found. An unquoted token is always closed.</param>
public sealed record MentionToken(MentionTrigger Trigger, string Query, int Start, int Length, bool IsQuoted, bool IsClosed)
{
    /// <summary>Gets the index just after the token.</summary>
    public int End => Start + Length;

    /// <summary>Gets whether the name ends with a slash, which asks for the contents of a folder.</summary>
    public bool IsFolderQuery => Query.Length > 0 && Query[^1] == '/';
}

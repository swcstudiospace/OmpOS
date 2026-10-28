using System.Text;

namespace OmpOS.Presentation.Mentions;

/// <summary>
/// Finds mentions in composer text. A mention is <c>@name</c>, <c>@folder/</c>, <c>@"a name with spaces"</c> or <c>#123</c>. The trigger opens a
/// mention only at the start of the text or after white space or an opening bracket, so an e-mail address or an anchor like <c>page#top</c> is
/// not a mention. Parsing is a pure function of the text.
/// </summary>
public static class MentionParser
{
    private const string OpeningBrackets = "([{";
    private const string TrailingPunctuation = ".,;:!?)]}'";

    /// <summary>
    /// Finds every complete mention of the text, for the send path. A trailing sentence punctuation mark is not part of an unquoted name, and a
    /// <c>#</c> opens a mention only when digits follow it (a pull request number). An unclosed quote is not a mention.
    /// </summary>
    /// <param name="text">The composer text.</param>
    /// <returns>The mentions in text order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static IReadOnlyList<MentionToken> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = new List<MentionToken>();
        var index = 0;
        while (index < text.Length)
        {
            if (!IsTriggerAt(text, index) || ReadToken(text, index, forSend: true) is not { } token)
            {
                index++;
                continue;
            }

            tokens.Add(token);
            index = Math.Max(token.End, index + 1);
        }

        return tokens;
    }

    /// <summary>
    /// Finds the mention the caret is in, for the picker: the token the user is typing. The caret must sit after the trigger and not past the end of the token.
    /// A <c>#</c> followed by anything but white space opens a mention so that a pull request can be searched by title.
    /// </summary>
    /// <param name="text">The composer text.</param>
    /// <param name="caret">The caret index, 0 to the text length.</param>
    /// <returns>The token under the caret, or <see langword="null"/> when the caret is not in a mention.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static MentionToken? FindActive(string text, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);

        caret = Math.Clamp(caret, 0, text.Length);
        // Walk back to the nearest trigger that is not separated from the caret by white space, unless a quote is open.
        for (var index = caret - 1; index >= 0; index--)
        {
            if (IsTriggerAt(text, index) && ReadToken(text, index, forSend: false) is { } token && caret > token.Start && caret <= TypingEnd(token, text))
            {
                return token;
            }

            if (char.IsWhiteSpace(text[index]) && !InsideOpenQuote(text, index, caret))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Builds the text of a mention as the composer writes it after the user picks a name: quoted when the name holds white space or ends with a punctuation mark that parsing would trim.</summary>
    /// <param name="trigger">The trigger.</param>
    /// <param name="name">The name to write.</param>
    /// <returns>For example <c>@src/App.cs</c> or <c>@"my notes.md"</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public static string Format(MentionTrigger trigger, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var mark = trigger == MentionTrigger.Hash ? '#' : '@';
        var needsQuotes = trigger == MentionTrigger.At
            && name.Length > 0
            && (name.Any(char.IsWhiteSpace) || TrailingPunctuation.Contains(name[^1], StringComparison.Ordinal));
        return needsQuotes
            ? new StringBuilder().Append(mark).Append('"').Append(name.Replace("\"", string.Empty, StringComparison.Ordinal)).Append('"').ToString()
            : mark + name;
    }

    private static int TypingEnd(MentionToken token, string text) =>
        token.IsQuoted && !token.IsClosed ? text.Length : token.End;

    private static bool InsideOpenQuote(string text, int whitespaceIndex, int caret)
    {
        // A quote opened by `@"` that has not closed before the caret keeps the white space inside the name.
        var open = text.LastIndexOf("@\"", whitespaceIndex, StringComparison.Ordinal);
        if (open < 0 || (open > 0 && !IsBoundary(text[open - 1])))
        {
            return false;
        }

        var close = text.IndexOf('"', open + 2);
        return close < 0 || close + 1 >= caret;
    }

    private static bool IsBoundary(char previous) => char.IsWhiteSpace(previous) || OpeningBrackets.Contains(previous, StringComparison.Ordinal);

    private static bool IsTriggerAt(string text, int index) =>
        (text[index] == '@' || text[index] == '#') && (index == 0 || IsBoundary(text[index - 1]));

    private static MentionToken? ReadToken(string text, int start, bool forSend)
    {
        var trigger = text[start] == '#' ? MentionTrigger.Hash : MentionTrigger.At;
        var nameStart = start + 1;

        if (trigger == MentionTrigger.At && nameStart < text.Length && text[nameStart] == '"')
        {
            var close = text.IndexOf('"', nameStart + 1);
            if (close < 0)
            {
                return forSend ? null : new MentionToken(trigger, text[(nameStart + 1)..], start, text.Length - start, true, false);
            }

            var quoted = text[(nameStart + 1)..close];
            return forSend && quoted.Length == 0 ? null : new MentionToken(trigger, quoted, start, close + 1 - start, true, true);
        }

        var end = nameStart;
        while (end < text.Length && !char.IsWhiteSpace(text[end]))
        {
            end++;
        }

        var query = text[nameStart..end];
        if (forSend)
        {
            if (trigger == MentionTrigger.Hash)
            {
                var digits = 0;
                while (digits < query.Length && char.IsAsciiDigit(query[digits]))
                {
                    digits++;
                }

                if (digits == 0)
                {
                    return null;
                }

                query = query[..digits];
                end = nameStart + digits;
            }
            else
            {
                query = TrimTrailingPunctuation(query);
                end = nameStart + query.Length;
                if (query.Length == 0)
                {
                    return null;
                }
            }
        }

        return new MentionToken(trigger, query, start, end - start, false, true);
    }

    private static string TrimTrailingPunctuation(string query)
    {
        var length = query.Length;
        while (length > 0 && TrailingPunctuation.Contains(query[length - 1], StringComparison.Ordinal))
        {
            length--;
        }

        return query[..length];
    }
}

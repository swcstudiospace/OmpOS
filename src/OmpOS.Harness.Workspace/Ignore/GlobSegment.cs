namespace OmpOS.Workspace.Ignore;

/// <summary>
/// One <c>/</c>-separated part of a rule: either <c>**</c> (any number of path segments) or a glob over the characters of one segment. Matching is iterative with a step
/// budget, so no pattern can take exponential time.
/// </summary>
internal sealed class GlobSegment
{
    private readonly GlobToken[] _tokens;
    private readonly string? _literal;

    private GlobSegment(bool isDoubleStar, GlobToken[] tokens, string? literal)
    {
        IsDoubleStar = isDoubleStar;
        _tokens = tokens;
        _literal = literal;
    }

    /// <summary>Gets the segment that stands for <c>**</c>.</summary>
    public static GlobSegment DoubleStar { get; } = new(isDoubleStar: true, [], literal: null);

    /// <summary>Gets whether this segment is <c>**</c>.</summary>
    public bool IsDoubleStar { get; }

    /// <summary>Creates a glob segment.</summary>
    /// <param name="tokens">The compiled elements.</param>
    /// <param name="literal">The segment text when every element is a literal character, otherwise <see langword="null"/>.</param>
    /// <returns>The segment.</returns>
    public static GlobSegment ForTokens(GlobToken[] tokens, string? literal) => new(isDoubleStar: false, tokens, literal);

    /// <summary>Tests one path segment against this glob.</summary>
    /// <param name="text">The path segment (it holds no <c>/</c>).</param>
    /// <param name="caseSensitive">Whether to compare with case.</param>
    /// <param name="budget">The remaining step budget; each step takes one, and a negative value after the call means the budget ran out and the result is unknown.</param>
    /// <returns><see langword="true"/> when the whole segment matches.</returns>
    public bool Matches(string text, bool caseSensitive, ref int budget)
    {
        if (_literal is not null)
        {
            budget--;
            return string.Equals(text, _literal, caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        }

        var tokenIndex = 0;
        var textIndex = 0;
        var starToken = -1;
        var starText = 0;
        while (textIndex < text.Length)
        {
            if (--budget < 0)
            {
                return false;
            }

            if (tokenIndex < _tokens.Length && _tokens[tokenIndex].Kind == GlobTokenKind.Star)
            {
                starToken = tokenIndex;
                starText = textIndex;
                tokenIndex++;
            }
            else if (tokenIndex < _tokens.Length && _tokens[tokenIndex].Matches(text[textIndex], caseSensitive))
            {
                tokenIndex++;
                textIndex++;
            }
            else if (starToken >= 0)
            {
                tokenIndex = starToken + 1;
                starText++;
                textIndex = starText;
            }
            else
            {
                return false;
            }
        }

        while (tokenIndex < _tokens.Length && _tokens[tokenIndex].Kind == GlobTokenKind.Star)
        {
            tokenIndex++;
        }

        return tokenIndex == _tokens.Length;
    }
}

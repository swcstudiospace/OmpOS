using System.Globalization;

namespace Autonogrammer.Workspace.Ignore;

/// <summary>One compiled element of a glob segment: a literal, <c>?</c>, <c>*</c> or a character class.</summary>
internal sealed class GlobToken
{
    private readonly char _literal;
    private readonly bool _negated;
    private readonly char[] _ranges;

    private GlobToken(GlobTokenKind kind, char literal, bool negated, char[] ranges)
    {
        Kind = kind;
        _literal = literal;
        _negated = negated;
        _ranges = ranges;
    }

    /// <summary>Gets the token that matches any run of characters.</summary>
    public static GlobToken Star { get; } = new(GlobTokenKind.Star, '\0', negated: false, []);

    /// <summary>Gets the token that matches any one character.</summary>
    public static GlobToken AnyCharacter { get; } = new(GlobTokenKind.AnyCharacter, '\0', negated: false, []);

    /// <summary>Gets what kind of element this is.</summary>
    public GlobTokenKind Kind { get; }

    /// <summary>Creates a token for one literal character.</summary>
    /// <param name="value">The character.</param>
    /// <param name="caseSensitive">Whether the character is compared with case; if not, it is stored folded.</param>
    /// <returns>The token.</returns>
    public static GlobToken ForLiteral(char value, bool caseSensitive) =>
        new(GlobTokenKind.Literal, caseSensitive ? value : Fold(value), negated: false, []);

    /// <summary>Creates a character-class token.</summary>
    /// <param name="negated">Whether the class matches the characters it does not list.</param>
    /// <param name="ranges">Inclusive range bounds, two characters per range (a single character repeats).</param>
    /// <returns>The token.</returns>
    public static GlobToken ForClass(bool negated, char[] ranges) =>
        new(GlobTokenKind.CharacterClass, '\0', negated, ranges);

    /// <summary>Tests one character against this token.</summary>
    /// <param name="value">The character from the path.</param>
    /// <param name="caseSensitive">Whether to compare with case.</param>
    /// <returns><see langword="true"/> when the token accepts the character; a <see cref="GlobTokenKind.Star"/> accepts every character.</returns>
    public bool Matches(char value, bool caseSensitive) =>
        Kind switch
        {
            GlobTokenKind.Literal => caseSensitive ? value == _literal : Fold(value) == _literal,
            GlobTokenKind.CharacterClass => InClass(value, caseSensitive) != _negated,
            _ => true,
        };

    private static char Fold(char value) => char.ToUpperInvariant(value);

    private static char Lower(char value) => CultureInfo.InvariantCulture.TextInfo.ToLower(value);

    private bool InClass(char value, bool caseSensitive) =>
        Listed(value) || (!caseSensitive && (Listed(Fold(value)) || Listed(Lower(value))));

    private bool Listed(char value)
    {
        for (var index = 0; index + 1 < _ranges.Length; index += 2)
        {
            if (value >= _ranges[index] && value <= _ranges[index + 1])
            {
                return true;
            }
        }

        return false;
    }
}

using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace OmpOS.Workspace.Ignore;

/// <summary>Compiles the text of one <c>/</c>-free pattern segment into a <see cref="GlobSegment"/>.</summary>
internal static class IgnorePatternCompiler
{
    /// <summary>Compiles one pattern segment.</summary>
    /// <param name="segment">
    /// The segment text. A backslash makes the next character literal; <c>*</c> and <c>**</c> inside a segment both match any run of characters; <c>?</c> matches one character;
    /// <c>[abc]</c>, <c>[a-z]</c> and <c>[!abc]</c> match one character from a set; an unclosed <c>[</c> is a literal. The text <c>**</c> alone is the any-directories segment.
    /// </param>
    /// <param name="caseSensitive">Whether the segment is compared with case.</param>
    /// <returns>The compiled segment.</returns>
    public static GlobSegment Compile(string segment, bool caseSensitive)
    {
        if (string.Equals(segment, "**", StringComparison.Ordinal))
        {
            return GlobSegment.DoubleStar;
        }

        var tokens = new List<GlobToken>(segment.Length);
        var literal = new StringBuilder(segment.Length);
        var allLiteral = true;
        var index = 0;
        while (index < segment.Length)
        {
            var current = segment[index];
            if (current == '*')
            {
                allLiteral = false;
                if (tokens.Count == 0 || tokens[^1].Kind != GlobTokenKind.Star)
                {
                    tokens.Add(GlobToken.Star);
                }

                index++;
            }
            else if (current == '?')
            {
                allLiteral = false;
                tokens.Add(GlobToken.AnyCharacter);
                index++;
            }
            else if (current == '[' && TryParseClass(segment, index, out var classToken, out var next))
            {
                allLiteral = false;
                tokens.Add(classToken);
                index = next;
            }
            else
            {
                var value = current == '\\' && index + 1 < segment.Length ? segment[index + 1] : current;
                index += current == '\\' && index + 1 < segment.Length ? 2 : 1;
                tokens.Add(GlobToken.ForLiteral(value, caseSensitive));
                literal.Append(value);
            }
        }

        return GlobSegment.ForTokens([.. tokens], allLiteral ? literal.ToString() : null);
    }

    private static bool TryParseClass(string segment, int start, [NotNullWhen(true)] out GlobToken? token, out int next)
    {
        token = null;
        next = start;
        var index = start + 1;
        var negated = false;
        if (index < segment.Length && (segment[index] == '!' || segment[index] == '^'))
        {
            negated = true;
            index++;
        }

        var ranges = new List<char>();
        var first = true;
        while (index < segment.Length)
        {
            var current = segment[index];
            if (current == ']' && !first)
            {
                token = GlobToken.ForClass(negated, [.. ranges]);
                next = index + 1;
                return true;
            }

            first = false;
            var low = ReadClassCharacter(segment, ref index);
            var high = low;
            if (index + 1 < segment.Length && segment[index] == '-' && segment[index + 1] != ']')
            {
                index++;
                high = ReadClassCharacter(segment, ref index);
            }

            ranges.Add(low);
            ranges.Add(high);
        }

        return false;
    }

    private static char ReadClassCharacter(string segment, ref int index)
    {
        var current = segment[index];
        if (current == '\\' && index + 1 < segment.Length)
        {
            index++;
            current = segment[index];
        }

        index++;
        return current;
    }
}

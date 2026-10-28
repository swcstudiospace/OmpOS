using System.Text;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// Turns untrusted text from stage events, task context and steering messages into plain display text: control characters and
/// bidirectional overrides are removed, whitespace controls become spaces, and text beyond a cap is cut with an ellipsis.
/// </summary>
internal static class PanelText
{
    /// <summary>The character that marks a cut.</summary>
    internal const char Ellipsis = '\u2026';

    /// <summary>Returns <paramref name="value"/> as plain display text of at most <paramref name="maxChars"/> characters plus the ellipsis.</summary>
    /// <param name="value">The untrusted text. Null or white space gives an empty string.</param>
    /// <param name="maxChars">The most characters kept before the ellipsis. At least 1.</param>
    /// <returns>The sanitised text. It is never null and never longer than <paramref name="maxChars"/> + 1 characters.</returns>
    internal static string Plain(string? value, int maxChars)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChars, 1);
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, maxChars) + 1);
        foreach (var character in value)
        {
            var mapped = Map(character);
            if (mapped is null)
            {
                continue;
            }

            if (builder.Length == maxChars)
            {
                if (char.IsHighSurrogate(builder[^1]))
                {
                    builder.Length--;
                }

                builder.Append(Ellipsis);
                break;
            }

            builder.Append(mapped.Value);
        }

        return builder.ToString().Trim();
    }

    private static char? Map(char character)
    {
        if (character is '\t' or '\n' or '\r' or '\u0085' or '\u2028' or '\u2029')
        {
            return ' ';
        }

        return char.IsControl(character) || IsInvisibleControl(character) ? null : character;
    }

    private static bool IsInvisibleControl(char character) =>
        character is '\u061C' or '\u200B' or '\u200E' or '\u200F' or '\uFEFF'
        || character is >= '\u202A' and <= '\u202E'
        || character is >= '\u2066' and <= '\u2069';
}

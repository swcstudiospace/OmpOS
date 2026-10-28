using System.Text;

namespace Autonogrammer.Harness.OmpConfig;

/// <summary>The few YAML scalar forms the managed block uses: a double-quoted string with only <c>\\</c> and <c>\"</c> escapes.</summary>
internal static class OmpYamlScalar
{
    /// <summary>
    /// Tells whether <paramref name="text"/> can be written inside a double-quoted scalar on one line and read back exactly:
    /// well-formed UTF-16, and no control character or Unicode line or paragraph separator.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>True when the text is plain.</returns>
    public static bool IsPlain(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (!Rune.TryGetRuneAt(text, index, out var rune))
            {
                return false;
            }

            if (Rune.IsControl(rune) ||
                Rune.GetUnicodeCategory(rune) is System.Globalization.UnicodeCategory.LineSeparator or System.Globalization.UnicodeCategory.ParagraphSeparator)
            {
                return false;
            }

            index += rune.Utf16SequenceLength;
        }

        return true;
    }

    /// <summary>Writes <paramref name="text"/> as a double-quoted scalar. The text must satisfy <see cref="IsPlain"/>.</summary>
    /// <param name="text">The plain text.</param>
    /// <returns>The quoted scalar.</returns>
    public static string Quote(string text) =>
        string.Concat("\"", text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal), "\"");

    /// <summary>
    /// Reads a scalar written by <see cref="Quote"/>. A scalar that is not double-quoted is returned as is, trimmed, so a block
    /// edited by hand to drop the quotes still reads.
    /// </summary>
    /// <param name="scalar">The scalar text after the key and its colon.</param>
    /// <param name="value">The decoded value.</param>
    /// <returns>False when the scalar is quoted but malformed or uses an escape other than <c>\\</c> and <c>\"</c>.</returns>
    public static bool TryUnquote(string scalar, out string value)
    {
        var text = scalar.Trim();
        if (text.Length == 0 || text[0] != '"')
        {
            value = text;
            return true;
        }

        var builder = new StringBuilder(text.Length);
        for (var index = 1; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                var rest = text[(index + 1)..].Trim();
                if (rest.Length > 0 && rest[0] != '#')
                {
                    break;
                }

                value = builder.ToString();
                return true;
            }

            if (character == '\\')
            {
                index++;
                if (index >= text.Length || text[index] is not ('\\' or '"'))
                {
                    break;
                }

                _ = builder.Append(text[index]);
                continue;
            }

            _ = builder.Append(character);
        }

        value = string.Empty;
        return false;
    }
}

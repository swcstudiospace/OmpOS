using System.Globalization;
using System.Text;

namespace Autonogrammer.Harness.AgentLoop.Stages;

/// <summary>
/// Cleans text that came from a vendor file before it goes into a stage event. A stage event carries plain text only, so
/// control, format and separator characters are replaced by a space and the length is capped.
/// </summary>
internal static class StageText
{
    /// <summary>The longest detail a stage event carries.</summary>
    public const int MaxDetailLength = 200;

    /// <summary>Returns <paramref name="value"/> without control characters, trimmed and cut to <paramref name="maxLength"/> characters.</summary>
    /// <param name="value">The raw text, or null.</param>
    /// <param name="maxLength">The longest result.</param>
    /// <returns>The cleaned text. Empty when <paramref name="value"/> is null or blank.</returns>
    public static string Clean(string? value, int maxLength = MaxDetailLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var ch in value)
        {
            if (builder.Length >= maxLength)
            {
                break;
            }

            if (!IsUnsafe(ch))
            {
                builder.Append(ch);
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                builder.Append(' ');
            }
        }

        if (builder.Length > 0 && char.IsHighSurrogate(builder[^1]))
        {
            builder.Length--;
        }

        return builder.ToString().Trim();
    }

    private static bool IsUnsafe(char ch) =>
        CharUnicodeInfo.GetUnicodeCategory(ch) is
            UnicodeCategory.Control or
            UnicodeCategory.Format or
            UnicodeCategory.LineSeparator or
            UnicodeCategory.ParagraphSeparator or
            UnicodeCategory.PrivateUse or
            UnicodeCategory.OtherNotAssigned;
}

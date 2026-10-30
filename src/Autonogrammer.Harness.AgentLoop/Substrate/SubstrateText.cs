using System.Globalization;
using System.Text;

namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>Cleans text that came from the Substrate server before a panel shows it. Server text is untrusted plain text.</summary>
internal static class SubstrateText
{
    /// <summary>
    /// Makes untrusted text safe to show as one line of plain text: line breaks and tabs become one space, other control
    /// characters, format characters (including bidirectional overrides and zero-width characters) and unpaired surrogates are
    /// removed, runs of spaces collapse, and the result is cut at <paramref name="maxLength"/> UTF-16 units without splitting a
    /// character.
    /// </summary>
    /// <param name="value">The text, or null.</param>
    /// <param name="maxLength">The most UTF-16 units the result may hold.</param>
    /// <returns>The cleaned text, empty when nothing is left.</returns>
    internal static string Sanitize(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || maxLength <= 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(Math.Min(value.Length, maxLength));
        var pendingSpace = false;
        var hasReplacement = value.Contains('\uFFFD', StringComparison.Ordinal);
        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value == Rune.ReplacementChar.Value && !hasReplacement)
            {
                continue;
            }

            var category = Rune.GetUnicodeCategory(rune);
            if (rune.Value is 0x0A or 0x0D or 0x09 or 0x20
                || category is UnicodeCategory.SpaceSeparator or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (category is UnicodeCategory.Format or UnicodeCategory.Control or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned)
            {
                continue;
            }

            var needed = rune.Utf16SequenceLength + (pendingSpace ? 1 : 0);
            if (builder.Length + needed > maxLength)
            {
                break;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }

    /// <summary>Removes the <c>[lessonbank]</c> header block (a marker line and <c>key: value</c> lines up to the first blank line) from a lesson body.</summary>
    /// <param name="body">The stored body.</param>
    /// <returns>The lesson text without the header.</returns>
    internal static string StripLessonHeader(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var lines = body.Split('\n');
        var first = lines.Length > 0 ? lines[0].Trim() : string.Empty;
        if (!first.StartsWith('[') || !first.EndsWith(']'))
        {
            return body;
        }

        var index = 1;
        while (index < lines.Length && lines[index].Trim().Length > 0)
        {
            index++;
        }

        return string.Join('\n', lines.Skip(index + 1));
    }
}

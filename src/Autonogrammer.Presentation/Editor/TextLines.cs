namespace Autonogrammer.Presentation.Editor;

/// <summary>The line split shared by the diffs and the hunk reviews, so that their line numbers always agree.</summary>
internal static class TextLines
{
    /// <summary>
    /// Splits a text into its lines, without the line break that ends the last one. The empty text has no lines, and a text ending in a line break
    /// has no extra empty line after it.
    /// </summary>
    /// <param name="text">The text to split.</param>
    /// <param name="endsWithNewline">True when the text ends with a line break. The empty text counts as ending with one.</param>
    /// <returns>The lines of the text.</returns>
    public static string[] Split(string text, out bool endsWithNewline)
    {
        if (text.Length == 0)
        {
            endsWithNewline = true;
            return [];
        }

        endsWithNewline = text[^1] == '\n';
        return (endsWithNewline ? text[..^1] : text).Split('\n');
    }
}

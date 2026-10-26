namespace OmpOS.Presentation.Settings.Customize;

/// <summary>
/// Derives the short description of a rule or skill from its markdown text, for the head that maps workspace files to
/// <see cref="CustomizeEntry"/> rows: the <c>description</c> key of a front-matter block when there is one, otherwise the first
/// line that is not a heading, a code fence or blank.
/// </summary>
public static class CustomizeDescription
{
    /// <summary>The longest description returned, in characters.</summary>
    public const int MaxLength = 160;

    private const string FrontMatterFence = "---";
    private const string DescriptionKey = "description:";

    /// <summary>Returns the short description of a markdown text.</summary>
    /// <param name="markdown">The file content.</param>
    /// <returns>The description, at most <see cref="MaxLength"/> characters; empty when the text has none.</returns>
    public static string FromMarkdown(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var lines = markdown.TrimStart('\uFEFF').Split('\n');
        var index = 0;
        if (string.Equals(lines[0].Trim(), FrontMatterFence, StringComparison.Ordinal))
        {
            var close = Array.FindIndex(lines, 1, line => string.Equals(line.Trim(), FrontMatterFence, StringComparison.Ordinal));
            if (close > 0)
            {
                for (var i = 1; i < close; i++)
                {
                    var key = lines[i].Trim();
                    if (key.StartsWith(DescriptionKey, StringComparison.OrdinalIgnoreCase))
                    {
                        var value = key[DescriptionKey.Length..].Trim().Trim('"', '\'');
                        if (value.Length > 0)
                        {
                            return Shorten(value);
                        }
                    }
                }

                index = close + 1;
            }
        }

        var inFence = false;
        for (; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (inFence || line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            return Shorten(line);
        }

        return string.Empty;
    }

    private static string Shorten(string text) => text.Length <= MaxLength ? text : string.Concat(text.AsSpan(0, MaxLength - 3), "...");
}

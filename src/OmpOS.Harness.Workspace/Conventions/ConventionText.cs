namespace OmpOS.Workspace.Conventions;

/// <summary>Small text rules shared by the convention readers: what a usable name is, and how a description is derived from a body.</summary>
internal static class ConventionText
{
    /// <summary>The longest description kept, in characters.</summary>
    public const int MaxDescriptionLength = 160;

    private const int MaxNameLength = 100;

    /// <summary>Tests whether a name can be typed after a slash: it starts with a letter or digit and holds only letters, digits, <c>-</c>, <c>_</c>, <c>.</c> and <c>:</c>.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when the name is usable.</returns>
    public static bool IsUsableName(string name)
    {
        if (name.Length == 0 || name.Length > MaxNameLength || !char.IsAsciiLetterOrDigit(name[0]))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the description of a markdown file: its frontmatter <c>description</c>, or else the first line of the body that is not a heading, a code fence or blank.</summary>
    /// <param name="markdown">The split file.</param>
    /// <returns>The description, at most <see cref="MaxDescriptionLength"/> characters; empty when there is none.</returns>
    public static string Describe(ConventionFrontmatter markdown) => Shorten(markdown.ReadText("description") ?? FirstLine(markdown.Body));

    /// <summary>Returns the first line of a text that is not a heading, a code fence or blank.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The shortened line; empty when there is none.</returns>
    public static string FirstLine(string text)
    {
        var inFence = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }
            else if (!inFence && line.Length > 0 && line[0] != '#')
            {
                return Shorten(line);
            }
        }

        return string.Empty;
    }

    /// <summary>Returns a name without its extension.</summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="extension">The extension, with its dot.</param>
    /// <returns>The file name without the extension, or <see langword="null"/> when the file name does not end with it.</returns>
    public static string? Stem(string fileName, string extension) =>
        fileName.Length > extension.Length && fileName.EndsWith(extension, StringComparison.Ordinal) ? fileName[..^extension.Length] : null;

    private static string Shorten(string text) =>
        text.Length <= MaxDescriptionLength ? text : string.Concat(text.AsSpan(0, MaxDescriptionLength - 3), "...");
}

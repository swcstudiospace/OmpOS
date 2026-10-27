namespace OmpOS.Workspace.Conventions;

/// <summary>
/// A small, bounded parser for the frontmatter block of a markdown file. It is not a YAML library: it reads the top-level keys that conventions use and never
/// interprets anchors, aliases or tags. A block that is not closed, is longer than <see cref="MaxLines"/> lines, or holds a line it cannot read is reported as a
/// problem and yields no fields.
/// </summary>
public static class ConventionFrontmatterParser
{
    /// <summary>The most lines a frontmatter block may hold.</summary>
    public const int MaxLines = 200;

    private const string Fence = "---";

    /// <summary>Splits a markdown text into frontmatter fields and body.</summary>
    /// <param name="text">The file text. A leading byte order mark is ignored.</param>
    /// <returns>The fields, the body and the problem, if any. A text that does not start with a <c>---</c> line has no frontmatter and is all body.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static ConventionFrontmatter Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var content = text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
        var lines = content.Split('\n');
        if (!IsFence(lines[0]))
        {
            return new ConventionFrontmatter(Empty(), content, null);
        }

        var close = FindClosingFence(lines);
        if (close < 0)
        {
            return Failure(content, "the frontmatter is not closed with '---' within " + MaxLines + " lines");
        }

        var (fields, problem) = ConventionFrontmatterBlock.Read(lines, 1, close);
        return problem is null
            ? new ConventionFrontmatter(fields, string.Join('\n', lines.Skip(close + 1)), null)
            : Failure(content, problem);
    }

    private static bool IsFence(string line) => string.Equals(line.TrimEnd(), Fence, StringComparison.Ordinal);

    private static int FindClosingFence(string[] lines)
    {
        var last = Math.Min(lines.Length - 1, MaxLines + 1);
        for (var index = 1; index <= last; index++)
        {
            if (IsFence(lines[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static Dictionary<string, ConventionFieldValue> Empty() => new(StringComparer.Ordinal);

    private static ConventionFrontmatter Failure(string content, string problem) => new(Empty(), content, problem);
}

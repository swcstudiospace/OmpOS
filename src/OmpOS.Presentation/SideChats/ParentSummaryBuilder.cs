using System.Text;
using OmpOS.Contracts.Ui;

namespace OmpOS.Presentation.SideChats;

/// <summary>
/// Builds the read-only summary of a parent session that a side chat is seeded with: what the user asked, what the assistant answered, the plan, the files it edited and the errors, newest last,
/// cut from the front to fit. It reads blocks and changes nothing; the summary is plain text with no instructions of its own.
/// </summary>
public static class ParentSummaryBuilder
{
    /// <summary>The default most characters of a summary.</summary>
    public const int DefaultMaxChars = 6000;

    private const int MaxLineChars = 600;

    /// <summary>Builds the summary of a transcript.</summary>
    /// <param name="blocks">The parent's blocks, in order.</param>
    /// <param name="maxChars">The most characters; older lines are dropped first.</param>
    /// <returns>The summary, or an empty string when the transcript has nothing to say.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="blocks"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxChars"/> is less than 100.</exception>
    public static string Build(IEnumerable<UiBlock> blocks, int maxChars = DefaultMaxChars)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxChars, 100);

        var lines = new List<string>();
        var edited = new List<string>();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case UserMessageBlock user:
                    Flush(lines, edited);
                    lines.Add("User: " + Shorten(user.Text));
                    break;
                case MarkdownBlock markdown when markdown.Markdown.Trim().Length > 0:
                    Flush(lines, edited);
                    lines.Add("Assistant: " + Shorten(markdown.Markdown));
                    break;
                case PlanBlock plan when plan.Steps.Count > 0:
                    Flush(lines, edited);
                    lines.Add("Plan: " + Shorten(string.Join("; ", plan.Steps)));
                    break;
                case FileEditBlock edit when !edited.Contains(edit.Path, StringComparer.Ordinal):
                    edited.Add(edit.Path);
                    break;
                case ErrorBlock error:
                    Flush(lines, edited);
                    lines.Add("Error: " + Shorten(error.Message));
                    break;
                default:
                    break;
            }
        }

        Flush(lines, edited);
        return Fit(lines, maxChars);
    }

    private static void Flush(List<string> lines, List<string> edited)
    {
        if (edited.Count > 0)
        {
            lines.Add("Edited files: " + Shorten(string.Join(", ", edited)));
            edited.Clear();
        }
    }

    private static string Shorten(string text)
    {
        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= MaxLineChars ? flat : flat[..MaxLineChars].TrimEnd() + "\u2026";
    }

    private static string Fit(List<string> lines, int maxChars)
    {
        var kept = new List<string>();
        var length = 0;
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            var cost = lines[index].Length + 1;
            if (length + cost > maxChars)
            {
                break;
            }

            kept.Add(lines[index]);
            length += cost;
        }

        kept.Reverse();
        var builder = new StringBuilder();
        foreach (var line in kept)
        {
            builder.Append(line).Append('\n');
        }

        return builder.ToString().TrimEnd('\n');
    }
}

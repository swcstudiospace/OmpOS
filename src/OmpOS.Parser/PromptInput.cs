using System.Text;
using System.Text.RegularExpressions;

namespace OmpOS.Parser;

/// <summary>
/// The normalized view of one prompt handed to every recognizer.
/// </summary>
/// <remarks>
/// <see cref="NormalizedText"/> has <c>\r\n</c> and <c>\r</c> folded to <c>\n</c>, C0/C1 control
/// characters removed (except <c>\n</c>) and surrounding whitespace trimmed. Fenced code blocks are
/// tracked per line; <see cref="ProseLines"/> additionally blanks fenced lines and masks inline code
/// spans and HTML tags, so keyword and mention scans cannot match inside them.
/// </remarks>
public sealed partial class PromptInput
{
    private const int MaskTimeoutMilliseconds = 1000;
    private readonly bool[] _fenced;

    private PromptInput(
        string rawText,
        string normalizedText,
        PromptContext context,
        IReadOnlyList<string> lines,
        IReadOnlyList<string> proseLines,
        bool[] fenced)
    {
        RawText = rawText;
        NormalizedText = normalizedText;
        Context = context;
        Lines = lines;
        ProseLines = proseLines;
        _fenced = fenced;
    }

    /// <summary>Gets the raw prompt as received (possibly truncated by the parser).</summary>
    public string RawText { get; }

    /// <summary>Gets the normalized prompt text.</summary>
    public string NormalizedText { get; }

    /// <summary>Gets the caller-supplied session context.</summary>
    public PromptContext Context { get; }

    /// <summary>Gets the normalized prompt split on <c>\n</c>.</summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>Gets the lines with fenced code blanked and inline code, HTML tags and comments masked.</summary>
    public IReadOnlyList<string> ProseLines { get; }

    /// <summary>Returns whether the normalized line is part of a triple-backtick fenced block (delimiters included).</summary>
    /// <param name="lineIndex">Zero-based line index into <see cref="Lines"/>.</param>
    /// <returns><see langword="true"/> when the line is inside a fence; otherwise <see langword="false"/>.</returns>
    public bool IsInsideCodeFence(int lineIndex) => (uint)lineIndex < (uint)_fenced.Length && _fenced[lineIndex];

    internal static PromptInput Create(string rawText, PromptContext context)
    {
        var normalized = Normalize(rawText);
        var lines = normalized.Split('\n');
        var fenced = MarkFencedLines(lines);

        var withoutFences = new string[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            withoutFences[i] = fenced[i] ? string.Empty : lines[i];
        }

        var prose = MaskNonProse(string.Join("\n", withoutFences)).Split('\n');
        return new PromptInput(rawText, normalized, context, lines, prose, fenced);
    }

    private static string Normalize(string rawText)
    {
        var builder = new StringBuilder(rawText.Length);
        for (var i = 0; i < rawText.Length; i++)
        {
            var c = rawText[i];
            if (c == '\r')
            {
                builder.Append('\n');
                if (i + 1 < rawText.Length && rawText[i + 1] == '\n')
                {
                    i++;
                }
            }
            else if (c == '\n' || !char.IsControl(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Trim();
    }

    private static bool[] MarkFencedLines(string[] lines)
    {
        var fenced = new bool[lines.Length];
        var open = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (open)
            {
                fenced[i] = true;
                open = !IsClosingFence(trimmed);
            }
            else if (IsOpeningFence(trimmed))
            {
                fenced[i] = true;
                open = true;
            }
        }

        return fenced;
    }

    private static bool IsOpeningFence(string trimmed) =>
        trimmed.StartsWith("```", StringComparison.Ordinal) && trimmed.IndexOf('`', 3) < 0;

    private static bool IsClosingFence(string trimmed) =>
        trimmed.StartsWith("```", StringComparison.Ordinal) && trimmed.Trim('`').Length == 0;

    private static string MaskNonProse(string text)
    {
        text = InlineCodeSpan().Replace(text, BlankMatch);
        text = HtmlComment().Replace(text, BlankMatch);
        return HtmlTag().Replace(text, BlankMatch);
    }

    private static string BlankMatch(Match match)
    {
        var chars = match.Value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] != '\n')
            {
                chars[i] = ' ';
            }
        }

        return new string(chars);
    }

    [GeneratedRegex("`[^`\\n]*`", RegexOptions.CultureInvariant, MaskTimeoutMilliseconds)]
    private static partial Regex InlineCodeSpan();

    [GeneratedRegex("<!--[\\s\\S]*?-->", RegexOptions.CultureInvariant, MaskTimeoutMilliseconds)]
    private static partial Regex HtmlComment();

    [GeneratedRegex("</?[A-Za-z][^>\\n]*>", RegexOptions.CultureInvariant, MaskTimeoutMilliseconds)]
    private static partial Regex HtmlTag();
}

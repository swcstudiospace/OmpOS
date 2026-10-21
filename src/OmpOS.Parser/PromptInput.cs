using System.Text;

namespace OmpOS.Parser;

/// <summary>
/// The normalized view of one prompt handed to every recognizer.
/// </summary>
/// <remarks>
/// <see cref="NormalizedText"/> has <c>\r\n</c> and <c>\r</c> folded to <c>\n</c>, C0/C1 control
/// characters removed (except <c>\n</c>) and surrounding whitespace trimmed. Fenced code blocks per
/// <c>FENCE_RE</c> (<c>^( {0,3})([`~]{3,})</c>, backtick or tilde) are tracked per line;
/// <see cref="ProseLines"/> is the length-preserving <c>maskNonProse</c> view where fenced code,
/// inline code spans (any backtick run length, closed by the same run length), balanced HTML/XML
/// elements including their content, and HTML comments are blanked, so keyword scans cannot match
/// inside them. An unbalanced <c>&lt;</c> is treated as literal text.
/// </remarks>
public sealed class PromptInput
{
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

    /// <summary>Gets the lines with fenced code, inline code spans, balanced elements and comments masked.</summary>
    public IReadOnlyList<string> ProseLines { get; }

    /// <summary>Returns whether the normalized line is part of a fenced code block per FENCE_RE (delimiters included).</summary>
    /// <param name="lineIndex">Zero-based line index into <see cref="Lines"/>.</param>
    /// <returns><see langword="true"/> when the line is inside a fence; otherwise <see langword="false"/>.</returns>
    public bool IsInsideCodeFence(int lineIndex) => (uint)lineIndex < (uint)_fenced.Length && _fenced[lineIndex];

    internal static PromptInput Create(string rawText, PromptContext context)
    {
        var normalized = Normalize(rawText);
        var lines = normalized.Split('\n');
        var fenced = MarkFencedLines(lines);
        var prose = MaskNonProse(normalized).Split('\n');
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
        var fenceChar = '\0';
        var fenceLen = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var hasFence = TryParseFence(line, 0, line.Length, out var indentLength, out var markerChar, out var markerLength);
            if (fenceChar != '\0')
            {
                fenced[i] = true;
                if (hasFence && markerChar == fenceChar && markerLength >= fenceLen && IsBlank(line, indentLength + markerLength, line.Length))
                {
                    fenceChar = '\0';
                    fenceLen = 0;
                }
            }
            else if (hasFence)
            {
                if (!(markerChar == '`' && ContainsBacktick(line, indentLength + markerLength, line.Length)))
                {
                    fenced[i] = true;
                    fenceChar = markerChar;
                    fenceLen = markerLength;
                }
            }
        }

        return fenced;
    }

    private static bool TryParseFence(string text, int lineStart, int lineEnd, out int indentLength, out char markerChar, out int markerLength)
    {
        indentLength = 0;
        markerChar = '\0';
        markerLength = 0;
        var indent = 0;
        while (indent < 3 && lineStart + indent < lineEnd && text[lineStart + indent] == ' ')
        {
            indent++;
        }

        indentLength = indent;
        var runStart = lineStart + indent;
        var runEnd = runStart;
        while (runEnd < lineEnd && (text[runEnd] == '`' || text[runEnd] == '~'))
        {
            runEnd++;
        }

        var length = runEnd - runStart;
        if (length < 3)
        {
            return false;
        }

        markerChar = text[runStart];
        markerLength = length;
        return true;
    }

    private static bool IsBlank(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsBacktick(string text, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (text[i] == '`')
            {
                return true;
            }
        }

        return false;
    }

    private static string MaskNonProse(string text)
    {
        if (!text.Contains('`', StringComparison.Ordinal) && !text.Contains('<', StringComparison.Ordinal) && !text.Contains("~~~", StringComparison.Ordinal))
        {
            return text;
        }

        var masked = new bool[text.Length];
        MaskFencedCodeBlocks(text, masked);
        MaskInlineCodeAndTags(text, masked);

        var chars = text.ToCharArray();
        for (var p = 0; p < chars.Length; p++)
        {
            if (masked[p] && chars[p] != '\n')
            {
                chars[p] = ' ';
            }
        }

        return new string(chars);
    }

    private static void MaskFencedCodeBlocks(string text, bool[] masked)
    {
        var n = text.Length;
        var fenceChar = '\0';
        var fenceLen = 0;
        var lineStart = 0;
        while (lineStart <= n)
        {
            var nl = text.IndexOf('\n', lineStart);
            if (nl < 0)
            {
                nl = n;
            }

            var hasFence = TryParseFence(text, lineStart, nl, out var indentLength, out var markerChar, out var markerLength);
            if (fenceChar != '\0')
            {
                for (var p = lineStart; p < nl; p++)
                {
                    masked[p] = true;
                }

                if (hasFence && markerChar == fenceChar && markerLength >= fenceLen && IsBlank(text, lineStart + indentLength + markerLength, nl))
                {
                    fenceChar = '\0';
                    fenceLen = 0;
                }
            }
            else if (hasFence)
            {
                if (!(markerChar == '`' && ContainsBacktick(text, lineStart + indentLength + markerLength, nl)))
                {
                    fenceChar = markerChar;
                    fenceLen = markerLength;
                    for (var p = lineStart; p < nl; p++)
                    {
                        masked[p] = true;
                    }
                }
            }

            if (nl == n)
            {
                break;
            }

            lineStart = nl + 1;
        }
    }

    private static void MaskInlineCodeAndTags(string text, bool[] masked)
    {
        var n = text.Length;
        var i = 0;
        while (i < n)
        {
            if (masked[i])
            {
                i++;
                continue;
            }

            var c = text[i];
            if (c == '`')
            {
                var runEnd = BacktickRunEnd(text, i, n);
                var close = FindBacktickClose(text, runEnd, n, runEnd - i, masked);
                if (close >= 0)
                {
                    for (var p = i; p < close; p++)
                    {
                        masked[p] = true;
                    }

                    i = close;
                }
                else
                {
                    i = runEnd;
                }

                continue;
            }

            if (c == '<')
            {
                var end = MaskTagAt(text, i, n, masked);
                i = end > i ? end : i + 1;
                continue;
            }

            i++;
        }
    }

    private static int BacktickRunEnd(string text, int start, int n)
    {
        var j = start;
        while (j < n && text[j] == '`')
        {
            j++;
        }

        return j;
    }

    private static int FindBacktickClose(string text, int from, int n, int runLength, bool[] masked)
    {
        var k = from;
        while (k < n)
        {
            if (masked[k])
            {
                k++;
                continue;
            }

            if (text[k] == '`')
            {
                var end = BacktickRunEnd(text, k, n);
                if (end - k == runLength)
                {
                    return end;
                }

                k = end;
                continue;
            }

            k++;
        }

        return -1;
    }

    private static int FindTagEnd(string text, int start, int n)
    {
        var quote = '\0';
        for (var k = start; k < n; k++)
        {
            var ch = text[k];
            if (quote != '\0')
            {
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch == '"' || ch == '\'')
            {
                quote = ch;
                continue;
            }

            if (ch == '>')
            {
                return k;
            }

            if (ch == '<')
            {
                return -1;
            }
        }

        return -1;
    }

    private static bool IsTagNameStart(char c) => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    private static bool IsTagNameChar(char c) => IsTagNameStart(c) || (c >= '0' && c <= '9') || c == '-';

    private static bool TryParseTagName(string text, int pos, int n, out string name, out int nextPos)
    {
        name = string.Empty;
        nextPos = pos;
        if (pos >= n || !IsTagNameStart(text[pos]))
        {
            return false;
        }

        var end = pos + 1;
        while (end < n && IsTagNameChar(text[end]))
        {
            end++;
        }

        name = text.Substring(pos, end - pos);
        nextPos = end;
        return true;
    }

    private static int FindMatchingClose(string text, int start, int n, string name, bool[] masked)
    {
        var depth = 1;
        var k = start;
        while (k < n)
        {
            if (masked[k] || text[k] != '<')
            {
                k++;
                continue;
            }

            var m = k + 1;
            var isClose = false;
            if (m < n && text[m] == '/')
            {
                isClose = true;
                m++;
            }

            if (!TryParseTagName(text, m, n, out var current, out var afterName))
            {
                k++;
                continue;
            }

            var gt = FindTagEnd(text, afterName, n);
            if (gt < 0)
            {
                k++;
                continue;
            }

            if (string.Equals(current, name, StringComparison.OrdinalIgnoreCase))
            {
                if (isClose)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return gt + 1;
                    }
                }
                else if (gt == 0 || text[gt - 1] != '/')
                {
                    depth++;
                }
            }

            k = gt + 1;
        }

        return -1;
    }

    private static int MaskTagAt(string text, int index, int n, bool[] masked)
    {
        if (index + 4 <= n && text[index] == '<' && text[index + 1] == '!' && text[index + 2] == '-' && text[index + 3] == '-')
        {
            var end = text.IndexOf("-->", index + 4, StringComparison.Ordinal);
            var stop = end < 0 ? n : end + 3;
            for (var p = index; p < stop; p++)
            {
                masked[p] = true;
            }

            return stop;
        }

        var j = index + 1;
        var closing = false;
        if (j < n && text[j] == '/')
        {
            closing = true;
            j++;
        }

        if (!TryParseTagName(text, j, n, out var name, out var afterName))
        {
            return index;
        }

        var gt = FindTagEnd(text, afterName, n);
        if (gt < 0)
        {
            return index;
        }

        var tagEnd = gt + 1;
        var selfClosing = gt > 0 && text[gt - 1] == '/';
        for (var p = index; p < tagEnd; p++)
        {
            masked[p] = true;
        }

        if (closing || selfClosing)
        {
            return tagEnd;
        }

        var close = FindMatchingClose(text, tagEnd, n, name, masked);
        if (close < 0)
        {
            return tagEnd;
        }

        for (var p = tagEnd; p < close; p++)
        {
            masked[p] = true;
        }

        return close;
    }
}

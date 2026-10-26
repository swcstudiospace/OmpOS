namespace OmpOS.Harness.OmpConfig;

/// <summary>
/// The result of looking at the text of <c>models.yml</c> without a YAML parser: its lines with their exact line endings, where the
/// root <c>providers:</c> mapping is, how its children are indented, which OmpOS blocks it holds and which provider ids exist outside
/// them. When the layout is one this editor does not touch, <see cref="Unsupported"/> says why and nothing else is trustworthy.
/// </summary>
internal sealed class OmpModelsLayout
{
    /// <summary>The text that starts a block marker comment, followed by the provider id.</summary>
    public const string BeginMarker = "# ompos:begin ";

    /// <summary>The text that ends a block marker comment, followed by the provider id.</summary>
    public const string EndMarker = "# ompos:end ";

    /// <summary>The comment OmpOS adds to a <c>providers:</c> line it created, so removing its last block removes the line again.</summary>
    public const string CreatedMarker = "# ompos:created";

    private const int DefaultIndent = 2;
    private const int MaxDefaultStep = 8;

    private readonly List<OmpYamlLine> _lines = [];
    private readonly List<OmpManagedBlockSpan> _blocks = [];
    private readonly HashSet<string> _foreignKeys = new(StringComparer.Ordinal);

    private OmpModelsLayout()
    {
    }

    /// <summary>Gets a value indicating whether the text starts with a byte order mark, which is kept out of <see cref="Lines"/>.</summary>
    public bool HasByteOrderMark { get; private set; }

    /// <summary>Gets the lines of the file.</summary>
    public IReadOnlyList<OmpYamlLine> Lines => _lines;

    /// <summary>Gets the line ending used for lines OmpOS adds: the first one the file uses, else <c>"\n"</c>.</summary>
    public string EolStyle { get; private set; } = "\n";

    /// <summary>Gets why the layout is not supported, or null when it is.</summary>
    public string? Unsupported { get; private set; }

    /// <summary>Gets the index of the root <c>providers:</c> line, or -1 when the file has none.</summary>
    public int ProvidersLine { get; private set; } = -1;

    /// <summary>Gets a value indicating whether the <c>providers:</c> line carries <see cref="CreatedMarker"/>.</summary>
    public bool ProvidersCreatedByOmpOs { get; private set; }

    /// <summary>Gets the index of the first line after the <c>providers:</c> section.</summary>
    public int SectionEnd { get; private set; }

    /// <summary>Gets the number of spaces the children of <c>providers:</c> are indented by.</summary>
    public int ChildIndent { get; private set; } = DefaultIndent;

    /// <summary>Gets the number of spaces one nesting level adds below the children.</summary>
    public int Step { get; private set; } = DefaultIndent;

    /// <summary>Gets the index after which a new block is inserted: the last significant line of the section, or the <c>providers:</c> line.</summary>
    public int InsertAfter { get; private set; } = -1;

    /// <summary>Gets the OmpOS blocks, in file order.</summary>
    public IReadOnlyList<OmpManagedBlockSpan> Blocks => _blocks;

    /// <summary>Gets the keys of <c>providers:</c> that are outside every OmpOS block.</summary>
    public IReadOnlySet<string> ForeignKeys => _foreignKeys;

    /// <summary>Looks at <paramref name="text"/>.</summary>
    /// <param name="text">The whole content of <c>models.yml</c>; empty for a missing file.</param>
    /// <returns>The layout; check <see cref="Unsupported"/> before using anything else.</returns>
    public static OmpModelsLayout Analyze(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var layout = new OmpModelsLayout();
        var body = text;
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            layout.HasByteOrderMark = true;
            body = text[1..];
        }

        if (!TrySplit(body, layout._lines))
        {
            layout.Unsupported = "The file uses a bare carriage return as a line ending. Edit models.yml by hand.";
            return layout;
        }

        foreach (var line in layout._lines)
        {
            if (line.Eol.Length > 0)
            {
                layout.EolStyle = line.Eol;
                break;
            }
        }

        layout.AnalyzeProviders();
        return layout;
    }

    /// <summary>Joins lines back into text, with the byte order mark this layout started with.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The text.</returns>
    public string Join(List<OmpYamlLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var builder = new System.Text.StringBuilder();
        if (HasByteOrderMark)
        {
            _ = builder.Append('\uFEFF');
        }

        foreach (var line in lines)
        {
            _ = builder.Append(line.Content).Append(line.Eol);
        }

        return builder.ToString();
    }

    /// <summary>Splits a <c>key: rest</c> line into its key and what follows the colon.</summary>
    /// <param name="content">The line, with or without indentation.</param>
    /// <param name="key">The key, unquoted.</param>
    /// <param name="rest">The text after the colon, untrimmed.</param>
    /// <returns>False when the line is not a simple mapping key line.</returns>
    public static bool TrySplitKey(string content, out string key, out string rest)
    {
        key = string.Empty;
        rest = string.Empty;
        var text = content.TrimStart(' ', '\t');
        if (text.Length == 0)
        {
            return false;
        }

        if (text[0] is '"' or '\'')
        {
            return TrySplitQuotedKey(text, out key, out rest);
        }

        if ((text[0] is '#' or '{' or '[' or '&' or '*' or '!' or '|' or '>' or '%' or '@' or '`' or '?') ||
            (text[0] == '-' && (text.Length == 1 || text[1] is ' ' or '\t')))
        {
            return false;
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '#' && index > 0 && text[index - 1] is ' ' or '\t')
            {
                return false;
            }

            if (text[index] == ':' && (index + 1 == text.Length || text[index + 1] is ' ' or '\t'))
            {
                var candidate = text[..index].TrimEnd();
                if (candidate.Length == 0)
                {
                    return false;
                }

                key = candidate;
                rest = text[(index + 1)..];
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns <paramref name="text"/> without a trailing YAML comment.</summary>
    /// <param name="text">A value with an optional comment.</param>
    /// <returns>The value part.</returns>
    public static string StripComment(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '#' && (index == 0 || text[index - 1] is ' ' or '\t'))
            {
                return text[..index];
            }
        }

        return text;
    }

    /// <summary>Tells whether a line holds only whitespace.</summary>
    /// <param name="content">The line.</param>
    /// <returns>True for an empty or whitespace-only line.</returns>
    public static bool IsBlank(string content) => string.IsNullOrWhiteSpace(content);

    private static bool TrySplit(string body, List<OmpYamlLine> lines)
    {
        var start = 0;
        for (var index = 0; index < body.Length; index++)
        {
            var character = body[index];
            if (character == '\r')
            {
                if (index + 1 >= body.Length || body[index + 1] != '\n')
                {
                    return false;
                }

                continue;
            }

            if (character != '\n')
            {
                continue;
            }

            var crlf = index > start && body[index - 1] == '\r';
            lines.Add(new OmpYamlLine(body[start..(crlf ? index - 1 : index)], crlf ? "\r\n" : "\n"));
            start = index + 1;
        }

        if (start < body.Length)
        {
            lines.Add(new OmpYamlLine(body[start..], string.Empty));
        }

        return true;
    }

    private static bool TrySplitQuotedKey(string text, out string key, out string rest)
    {
        key = string.Empty;
        rest = string.Empty;
        var quote = text[0];
        var builder = new System.Text.StringBuilder();
        var index = 1;
        var closed = false;
        while (index < text.Length)
        {
            var character = text[index];
            if (quote == '"' && character == '\\' && index + 1 < text.Length)
            {
                _ = builder.Append(text[index + 1]);
                index += 2;
                continue;
            }

            if (character == quote)
            {
                if (quote == '\'' && index + 1 < text.Length && text[index + 1] == '\'')
                {
                    _ = builder.Append('\'');
                    index += 2;
                    continue;
                }

                closed = true;
                index++;
                break;
            }

            _ = builder.Append(character);
            index++;
        }

        if (!closed)
        {
            return false;
        }

        while (index < text.Length && text[index] is ' ' or '\t')
        {
            index++;
        }

        if (index >= text.Length || text[index] != ':' || (index + 1 < text.Length && text[index + 1] is not (' ' or '\t')))
        {
            return false;
        }

        key = builder.ToString();
        rest = text[(index + 1)..];
        return true;
    }

    private static bool IsRootKeyLine(string content) =>
        content.Length > 0 && content[0] is not (' ' or '\t' or '#');

    private static bool IsDocumentMarker(string content) =>
        (content.StartsWith("---", StringComparison.Ordinal) || content.StartsWith("...", StringComparison.Ordinal)) &&
        (content.Length == 3 || content[3] is ' ' or '\t');

    private static int Indent(string content)
    {
        var index = 0;
        while (index < content.Length && content[index] is ' ' or '\t')
        {
            index++;
        }

        return index;
    }

    private static bool TryMarker(string trimmed, out bool isBegin, out string id)
    {
        isBegin = false;
        id = string.Empty;
        var text = trimmed.TrimEnd();
        if (text.StartsWith(BeginMarker, StringComparison.Ordinal))
        {
            isBegin = true;
            id = text[BeginMarker.Length..];
        }
        else if (text.StartsWith(EndMarker, StringComparison.Ordinal))
        {
            id = text[EndMarker.Length..];
        }
        else
        {
            return false;
        }

        return OmpProviderId.IsValid(id);
    }

    private void AnalyzeProviders()
    {
        for (var index = 0; index < _lines.Count; index++)
        {
            var content = _lines[index].Content;
            if (IsDocumentMarker(content))
            {
                Unsupported = "The file has a YAML document marker (--- or ...). Edit models.yml by hand.";
                return;
            }

            if (!IsRootKeyLine(content) || !TrySplitKey(content, out var key, out var rest) ||
                !string.Equals(key, "providers", StringComparison.Ordinal))
            {
                continue;
            }

            if (ProvidersLine >= 0)
            {
                Unsupported = "The file defines providers: more than once. Edit models.yml by hand.";
                return;
            }

            ProvidersLine = index;
            ProvidersCreatedByOmpOs = rest.Contains(CreatedMarker, StringComparison.Ordinal);
            var value = StripComment(rest).Trim();
            if (value.Length > 0)
            {
                Unsupported = value[0] is '{' or '['
                    ? "providers: is written in flow style (for example providers: {}). Edit models.yml by hand, or rewrite providers: as a block mapping."
                    : "providers: is not a block mapping. Edit models.yml by hand.";
                return;
            }
        }

        if (ProvidersLine < 0)
        {
            SectionEnd = _lines.Count;
            return;
        }

        SectionEnd = ProvidersLine + 1;
        while (SectionEnd < _lines.Count && !IsRootKeyLine(_lines[SectionEnd].Content))
        {
            SectionEnd++;
        }

        if (SectionEnd < _lines.Count && _lines[SectionEnd].Content.StartsWith('-'))
        {
            Unsupported = "providers: is a sequence, not a mapping. Edit models.yml by hand.";
            return;
        }

        AnalyzeSection();
    }

    private void AnalyzeSection()
    {
        var scan = new SectionScan();
        for (var index = ProvidersLine + 1; index < SectionEnd; index++)
        {
            if (!ScanLine(index, scan))
            {
                return;
            }
        }

        if (scan.ChildIndent >= 0)
        {
            ChildIndent = scan.ChildIndent;
            Step = scan.Step > 0 ? scan.Step : scan.ChildIndent <= MaxDefaultStep ? scan.ChildIndent : DefaultIndent;
        }

        if (!TryPairMarkers(scan.Markers))
        {
            return;
        }

        CollectForeignKeys();
        InsertAfter = LastSignificantLine();
    }

    /// <summary>Reads one line of the providers: section into <paramref name="scan"/>; false when the section is unsupported (<see cref="Unsupported"/> says why).</summary>
    private bool ScanLine(int index, SectionScan scan)
    {
        var content = _lines[index].Content;
        if (IsBlank(content))
        {
            return true;
        }

        var indent = Indent(content);
        var trimmed = content[indent..];
        var isComment = trimmed[0] == '#';
        var leading = content[..indent];
        if (!isComment && leading.Contains('\t', StringComparison.Ordinal))
        {
            Unsupported = "The providers: section is indented with tabs. Edit models.yml by hand.";
            return false;
        }

        var isMarker = isComment && TryMarker(trimmed, out _, out _);
        if (isComment && !isMarker)
        {
            return true;
        }

        if (scan.ChildIndent < 0)
        {
            Unsupported = FirstChildProblem(indent, leading, trimmed, isMarker);
            if (Unsupported is not null)
            {
                return false;
            }

            scan.ChildIndent = indent;
        }

        if (isMarker)
        {
            if (indent != scan.ChildIndent)
            {
                Unsupported = "An ompos marker comment is not at the indentation of the providers: children. Edit models.yml by hand.";
                return false;
            }

            _ = TryMarker(trimmed, out var isBegin, out var markerId);
            scan.Markers.Add((index, isBegin, markerId));
            return true;
        }

        if (indent < scan.ChildIndent)
        {
            Unsupported = "The providers: section has inconsistent indentation. Edit models.yml by hand.";
            return false;
        }

        if (indent > scan.ChildIndent && scan.Step < 0)
        {
            scan.Step = indent - scan.ChildIndent;
        }

        return true;
    }

    private static string? FirstChildProblem(int indent, string leading, string trimmed, bool isMarker)
    {
        if (indent == 0 || leading.Contains('\t', StringComparison.Ordinal))
        {
            return "A marker comment is not indented as a child of providers:. Edit models.yml by hand.";
        }

        if (!isMarker && ((trimmed[0] is '{' or '[') || (trimmed[0] == '-' && (trimmed.Length == 1 || trimmed[1] is ' ' or '\t'))))
        {
            return "providers: does not hold a block mapping. Edit models.yml by hand.";
        }

        return null;
    }

    private bool TryPairMarkers(List<(int Index, bool IsBegin, string Id)> markers)
    {
        string? openId = null;
        var openBegin = -1;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (index, isBegin, id) in markers)
        {
            if (isBegin)
            {
                if (openId is not null)
                {
                    Unsupported = $"The ompos block for {id} starts inside the block for {openId}. Edit models.yml by hand.";
                    return false;
                }

                if (!seen.Add(id))
                {
                    Unsupported = $"The file holds two ompos blocks for {id}. Edit models.yml by hand.";
                    return false;
                }

                openId = id;
                openBegin = index;
                continue;
            }

            if (openId is null || !string.Equals(openId, id, StringComparison.Ordinal))
            {
                Unsupported = $"The ompos end marker for {id} has no matching begin marker. Edit models.yml by hand.";
                return false;
            }

            _blocks.Add(new OmpManagedBlockSpan(id, openBegin, index));
            openId = null;
        }

        if (openId is not null)
        {
            Unsupported = $"The ompos block for {openId} has no end marker. Edit models.yml by hand.";
            return false;
        }

        return true;
    }

    private void CollectForeignKeys()
    {
        for (var index = ProvidersLine + 1; index < SectionEnd; index++)
        {
            var content = _lines[index].Content;
            if (IsBlank(content) || InsideBlock(index) || Indent(content) != ChildIndent || content[ChildIndent] == '#')
            {
                continue;
            }

            if (TrySplitKey(content, out var key, out _))
            {
                _ = _foreignKeys.Add(key);
            }
        }
    }

    private bool InsideBlock(int index)
    {
        foreach (var block in _blocks)
        {
            if (index >= block.Begin && index <= block.End)
            {
                return true;
            }
        }

        return false;
    }

    private int LastSignificantLine()
    {
        for (var index = SectionEnd - 1; index > ProvidersLine; index--)
        {
            var content = _lines[index].Content;
            if (IsBlank(content) || content[0] == '#')
            {
                continue;
            }

            return index;
        }

        return ProvidersLine;
    }

    private sealed class SectionScan
    {
        public List<(int Index, bool IsBegin, string Id)> Markers { get; } = [];

        public int ChildIndent { get; set; } = -1;

        public int Step { get; set; } = -1;
    }
}

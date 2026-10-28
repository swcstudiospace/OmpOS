using System.Text.RegularExpressions;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Phase 25.9 suppression ledger scan, shared by the repository gate and its controls.
/// Finds every <c>#pragma warning disable</c>, <c>SuppressMessage</c> attribute and
/// <c>.editorconfig</c> severity override set to none in first-party .cs, .razor, .axaml and
/// .editorconfig files, and compares them with the kept SUP rows of docs/open-questions.md.
/// The match key is the repository-relative file path plus the rule id; line numbers are never
/// part of the key, so edits above a suppression do not invalidate its row. Comments and string
/// literals are blanked before matching, so text that merely mentions a suppression is ignored.
/// Skipped path segments: .git, bin, obj, node_modules, TestResults, output, .planning, docs,
/// TestData, .nuget and plugins (checkouts of other repositories and generated output).
/// </summary>
internal static class SuppressionScanner
{
    private const string KeptStatusPrefix = "kept";

    private const string RowQuestionPrefix = "Is the suppression `";

    private static readonly string[] ScannedExtensions = [".cs", ".razor", ".axaml", ".editorconfig"];

    private static readonly string[] ExcludedSegments =
    [
        ".git", "bin", "obj", "node_modules", "TestResults", "output",
        ".planning", "docs", "TestData", ".nuget", "plugins",
    ];

    private static readonly (string Open, string Close)[] CSharpBlockComments = [("/*", "*/")];

    private static readonly (string Open, string Close)[] RazorBlockComments = [("/*", "*/"), ("@*", "*@"), ("<!--", "-->")];

    private static readonly (string Open, string Close)[] XamlBlockComments = [("<!--", "-->")];

    private static readonly Regex PragmaPattern = new(@"^\s*#\s*pragma\s+warning\s+disable\b(?<ids>.*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

    private static readonly Regex RuleIdPattern = new(@"[A-Za-z]*[0-9]+", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex SuppressMessageCallPattern = new(@"\bSuppressMessage(?:Attribute)?\s*\(", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex CheckIdPattern = new(@"^SuppressMessage(?:Attribute)?\s*\(\s*(?:category\s*:\s*)?""[^""]*""\s*,\s*(?:checkId\s*:\s*)?""(?<id>[^"":]+)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex SeverityNonePattern = new(@"^\s*dotnet_(?:diagnostic|analyzer_diagnostic)(?:\.(?<key>[^\s=]+?))?\.severity\s*=\s*none\s*(?:[#;].*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));

    private static readonly Regex BacktickSpanPattern = new(@"`(?<span>[^`]+)`", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    /// <summary>
    /// Scans first-party source under <paramref name="repoRoot"/> and returns every suppression found,
    /// ordered by path and line.
    /// </summary>
    /// <param name="repoRoot">The repository root, or a temporary tree shaped like one.</param>
    /// <returns>One entry per suppressed rule at each site.</returns>
    public static List<SuppressionOccurrence> Scan(string repoRoot)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);
        var found = new List<SuppressionOccurrence>();
        foreach (var file in EnumerateScanFiles(repoRoot))
        {
            var relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            var lines = File.ReadAllLines(file);
            if (string.Equals(Path.GetFileName(file), ".editorconfig", StringComparison.Ordinal))
            {
                ScanEditorConfig(relative, lines, found);
            }
            else
            {
                ScanSource(relative, Path.GetExtension(file), lines, found);
            }
        }

        return found;
    }

    /// <summary>
    /// Reads the kept SUP rows of the open-questions table. A kept row has an ID starting with SUP-,
    /// a Status starting with "kept" and a question of the form
    /// <c>Is the suppression `RULE` in `path` still needed?</c>; the first backtick span is the rule
    /// and the second is the repository-relative path.
    /// </summary>
    /// <param name="questionsPath">The path of docs/open-questions.md.</param>
    /// <returns>One entry per kept suppression row.</returns>
    public static List<(string Id, string Path, string Rule)> ReadKeptRows(string questionsPath)
    {
        ArgumentNullException.ThrowIfNull(questionsPath);
        var rows = new List<(string Id, string Path, string Rule)>();
        foreach (var line in File.ReadAllLines(questionsPath))
        {
            if (!line.StartsWith("| SUP-", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|');
            if (cells.Length < 6 || !cells[^1].Trim().StartsWith(KeptStatusPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var question = cells[1].Trim();
            if (!question.StartsWith(RowQuestionPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var spans = BacktickSpanPattern.Matches(question);
            if (spans.Count >= 2)
            {
                rows.Add((cells[0].Trim(), spans[1].Groups["span"].Value.Trim().Replace('\\', '/'), spans[0].Groups["span"].Value.Trim()));
            }
        }

        return rows;
    }

    /// <summary>
    /// Returns one message per file-and-rule pair that has more suppressions in source than kept rows.
    /// </summary>
    /// <param name="occurrences">The suppressions found in source.</param>
    /// <param name="rows">The kept SUP rows.</param>
    /// <returns>Messages naming each unlisted suppression; empty when every suppression has its row.</returns>
    public static List<string> FindUnlisted(IReadOnlyList<SuppressionOccurrence> occurrences, IReadOnlyList<(string Id, string Path, string Rule)> rows)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(rows);
        var rowCounts = CountRows(rows);
        var offenders = new List<string>();
        foreach (var group in occurrences.GroupBy(static o => KeyOf(o.Path, o.RuleId), StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(static o => o.Line).ToList();
            var listed = rowCounts.GetValueOrDefault(group.Key);
            foreach (var extra in ordered.Skip(listed))
            {
                offenders.Add($"{extra.Path}:{extra.Line}: {extra.Kind} {extra.RuleId} has no kept SUP row in docs/open-questions.md ({ordered.Count} in source, {listed} listed)");
            }
        }

        return offenders;
    }

    /// <summary>
    /// Returns one message per kept SUP row that no suppression in source backs any longer.
    /// </summary>
    /// <param name="occurrences">The suppressions found in source.</param>
    /// <param name="rows">The kept SUP rows.</param>
    /// <returns>Messages naming each orphaned row; empty when every kept row has its suppression.</returns>
    public static List<string> FindOrphanRows(IReadOnlyList<SuppressionOccurrence> occurrences, IReadOnlyList<(string Id, string Path, string Rule)> rows)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        ArgumentNullException.ThrowIfNull(rows);
        var sourceCounts = occurrences.GroupBy(static o => KeyOf(o.Path, o.RuleId), StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var group in rows.GroupBy(static r => KeyOf(r.Path, r.Rule), StringComparer.Ordinal))
        {
            var inSource = sourceCounts.GetValueOrDefault(group.Key);
            foreach (var extra in group.Skip(inSource))
            {
                offenders.Add($"{extra.Id}: kept row for {extra.Rule} in {extra.Path} has no matching suppression ({inSource} in source); mark it fixed or remove the stale row");
            }
        }

        return offenders;
    }

    private static Dictionary<string, int> CountRows(IReadOnlyList<(string Id, string Path, string Rule)> rows)
    {
        return rows.GroupBy(static r => KeyOf(r.Path, r.Rule), StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.Ordinal);
    }

    private static string KeyOf(string path, string rule)
    {
        return string.Concat(path, "|", rule.ToUpperInvariant());
    }

    private static List<string> EnumerateScanFiles(string repoRoot)
    {
        var files = new List<string>();
        var stack = new Stack<string>();
        stack.Push(repoRoot);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var subdir in Directory.GetDirectories(dir))
            {
                if (!ExcludedSegments.Contains(Path.GetFileName(subdir), StringComparer.Ordinal))
                {
                    stack.Push(subdir);
                }
            }

            foreach (var file in Directory.GetFiles(dir))
            {
                if (ScannedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    files.Add(file);
                }
            }
        }

        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static void ScanEditorConfig(string relative, string[] lines, List<SuppressionOccurrence> found)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var match = SeverityNonePattern.Match(lines[i]);
            if (match.Success)
            {
                var key = match.Groups["key"];
                found.Add(new SuppressionOccurrence(relative, i + 1, "editorconfig", key.Success ? key.Value : "*"));
            }
        }
    }

    private static void ScanSource(string relative, string extension, string[] lines, List<SuppressionOccurrence> found)
    {
        var state = new LexState();
        for (var i = 0; i < lines.Length; i++)
        {
            var code = Sanitize(lines[i], extension, state);
            var pragma = PragmaPattern.Match(code);
            if (pragma.Success)
            {
                AddPragmaRules(relative, i + 1, pragma.Groups["ids"].Value, found);
            }

            foreach (Match call in SuppressMessageCallPattern.Matches(code))
            {
                found.Add(new SuppressionOccurrence(relative, i + 1, "SuppressMessage", ReadCheckId(lines, i, call.Index)));
            }
        }
    }

    private static void AddPragmaRules(string relative, int line, string ids, List<SuppressionOccurrence> found)
    {
        var matches = RuleIdPattern.Matches(ids);
        if (matches.Count == 0)
        {
            found.Add(new SuppressionOccurrence(relative, line, "pragma", "*"));
            return;
        }

        foreach (Match match in matches)
        {
            found.Add(new SuppressionOccurrence(relative, line, "pragma", match.Value));
        }
    }

    private static string ReadCheckId(string[] lines, int lineIndex, int column)
    {
        var tail = new List<string> { lines[lineIndex][column..] };
        for (var next = lineIndex + 1; next < lines.Length && next <= lineIndex + 5; next++)
        {
            tail.Add(lines[next]);
        }

        var match = CheckIdPattern.Match(string.Join(' ', tail));
        return match.Success ? match.Groups["id"].Value.Trim() : "?";
    }

    /// <summary>
    /// Returns <paramref name="line"/> with comments and string and character literals replaced by
    /// spaces, so the result has the same length and column positions as the original.
    /// </summary>
    private static string Sanitize(string line, string extension, LexState state)
    {
        var chars = line.ToCharArray();
        var isCSharp = string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase);
        var lineComments = !string.Equals(extension, ".axaml", StringComparison.OrdinalIgnoreCase);
        var blockComments = BlockCommentsFor(extension);
        var i = 0;
        while (i < chars.Length)
        {
            var carried = ConsumeCarriedState(line, chars, i, state);
            if (carried != i)
            {
                i = carried;
                continue;
            }

            if (lineComments && line[i] == '/' && i + 1 < line.Length && line[i + 1] == '/')
            {
                Blank(chars, i, chars.Length);
                break;
            }

            var opened = false;
            foreach (var (open, close) in blockComments)
            {
                if (string.CompareOrdinal(line, i, open, 0, open.Length) == 0)
                {
                    state.BlockCommentEnd = close;
                    Blank(chars, i, i + open.Length);
                    i += open.Length;
                    opened = true;
                    break;
                }
            }

            if (opened)
            {
                continue;
            }

            i = line[i] switch
            {
                '"' => ConsumeString(line, chars, i, state, isCSharp),
                '\'' => ConsumeCharLiteral(line, chars, i),
                _ => i + 1,
            };
        }

        return new string(chars);
    }

    private static (string Open, string Close)[] BlockCommentsFor(string extension)
    {
        if (string.Equals(extension, ".razor", StringComparison.OrdinalIgnoreCase))
        {
            return RazorBlockComments;
        }

        return string.Equals(extension, ".axaml", StringComparison.OrdinalIgnoreCase) ? XamlBlockComments : CSharpBlockComments;
    }

    /// <summary>
    /// Continues a block comment, raw string or verbatim string that started on an earlier line.
    /// Returns the index after the consumed text, or <paramref name="index"/> when none is open.
    /// </summary>
    private static int ConsumeCarriedState(string line, char[] chars, int index, LexState state)
    {
        if (state.BlockCommentEnd is { } end)
        {
            var close = line.IndexOf(end, index, StringComparison.Ordinal);
            var stop = close < 0 ? chars.Length : close + end.Length;
            Blank(chars, index, stop);
            state.BlockCommentEnd = close < 0 ? end : null;
            return stop;
        }

        if (state.InRawString)
        {
            var close = line.IndexOf("\"\"\"", index, StringComparison.Ordinal);
            var stop = close < 0 ? chars.Length : close + 3;
            Blank(chars, index, stop);
            state.InRawString = close < 0;
            return stop;
        }

        if (state.InVerbatimString)
        {
            var stop = FindVerbatimEnd(line, index);
            Blank(chars, index, stop < 0 ? chars.Length : stop);
            state.InVerbatimString = stop < 0;
            return stop < 0 ? chars.Length : stop;
        }

        return index;
    }

    private static int ConsumeString(string line, char[] chars, int start, LexState state, bool isCSharp)
    {
        if (isCSharp && string.CompareOrdinal(line, start, "\"\"\"", 0, 3) == 0)
        {
            var close = line.IndexOf("\"\"\"", start + 3, StringComparison.Ordinal);
            var stop = close < 0 ? chars.Length : close + 3;
            Blank(chars, start, stop);
            state.InRawString = close < 0;
            return stop;
        }

        if (isCSharp && start > 0 && (line[start - 1] == '@' || (start > 1 && line[start - 1] == '$' && line[start - 2] == '@')))
        {
            var stop = FindVerbatimEnd(line, start + 1);
            Blank(chars, start, stop < 0 ? chars.Length : stop);
            state.InVerbatimString = stop < 0;
            return stop < 0 ? chars.Length : stop;
        }

        var end = start + 1;
        while (end < line.Length && line[end] != '"')
        {
            end += line[end] == '\\' ? 2 : 1;
        }

        var after = Math.Min(end + 1, chars.Length);
        Blank(chars, start, after);
        return after;
    }

    /// <summary>Returns the index after the closing quote of a verbatim string body starting at <paramref name="from"/>, or -1 when it continues on the next line.</summary>
    private static int FindVerbatimEnd(string line, int from)
    {
        var j = from;
        while (j < line.Length)
        {
            if (line[j] != '"')
            {
                j++;
            }
            else if (j + 1 < line.Length && line[j + 1] == '"')
            {
                j += 2;
            }
            else
            {
                return j + 1;
            }
        }

        return -1;
    }

    private static int ConsumeCharLiteral(string line, char[] chars, int start)
    {
        var length = 1;
        if (start + 2 < line.Length && line[start + 1] != '\\' && line[start + 2] == '\'')
        {
            length = 3;
        }
        else if (start + 3 < line.Length && line[start + 1] == '\\' && line[start + 3] == '\'')
        {
            length = 4;
        }

        Blank(chars, start, start + length);
        return start + length;
    }

    private static void Blank(char[] chars, int from, int to)
    {
        var end = Math.Min(to, chars.Length);
        for (var k = from; k < end; k++)
        {
            chars[k] = ' ';
        }
    }

    private sealed class LexState
    {
        public string? BlockCommentEnd { get; set; }

        public bool InRawString { get; set; }

        public bool InVerbatimString { get; set; }
    }
}

using System.Text.RegularExpressions;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// VERIFY-02 TODO-marker scan, shared by the repository gate and its positive and negative controls.
/// </summary>
internal static class TodoMarkerScanner
{
    /// <summary>
    /// VERIFY-02 rule: every whole-word, uppercase TODO, FIXME, HACK or XXX token in
    /// first-party code must share its line with a valid open-question ID (OQ-NN or
    /// Q-UPPER-... with no letter, digit or hyphen directly before or after) whose
    /// docs/open-questions.md table row (ID | Question | Why it matters | Default taken
    /// | Owner | Status) carries a non-empty Owner. Markers are detected anywhere on a
    /// line of first-party code, whether or not a comment opener precedes them, so a bare
    /// continuation line inside a block comment still counts. Only uppercase tokens count,
    /// by convention; lowercase todo/fixme never match. A "//" inside a string literal can
    /// produce a match and is accepted as flagged (no string-literal stripping: the gate
    /// favors false positives over silent bypass).
    /// Scanned extensions (first-party code): .cs, .razor, .csproj, .props, .targets, .sh,
    /// .yml, .yaml, .json, .slnf, .slnx, .css, .js, .ts, .tsx, .html.
    /// Skipped path segments: .git, bin, obj, node_modules, TestResults, output, .planning
    /// (planning text is not code), docs (documentation, not code), TestData (recorded
    /// payloads are data), .nuget (the NuGet package cache, which CI places inside the
    /// checkout), plus the vendored wwwroot/lib subtree only (wwwroot itself stays
    /// scanned: first-party css and html live there).
    /// </summary>
    private static readonly string[] TodoScanExtensions =
    [
        ".cs", ".razor", ".csproj", ".props", ".targets", ".sh", ".yml", ".yaml",
        ".json", ".slnf", ".slnx", ".css", ".js", ".ts", ".tsx", ".html",
    ];

    /// <summary>
    /// Path segments skipped by the VERIFY-02 scan (see the rule documented on
    /// <see cref="TodoScanExtensions"/>). The vendored wwwroot/lib subtree is handled
    /// separately in <see cref="IsExcludedScanPath"/> so that wwwroot itself stays scanned.
    /// </summary>
    private static readonly string[] TodoExcludedScanSegments =
    [
        ".git", "bin", "obj", "node_modules", "TestResults", "output",
        ".planning", "docs", "TestData", ".nuget",
    ];

    private static readonly Regex MarkerPattern = new(@"\b(?:TODO|FIXME|HACK|XXX)\b", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex OpenQuestionIdPattern = new(@"(?<![A-Za-z0-9-])(?:OQ-[0-9]+|Q-[A-Z][A-Z0-9-]*)(?![A-Za-z0-9-])", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    public static Dictionary<string, string> ParseOpenQuestionOwners(string questionsPath)
    {
        var ownerById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(questionsPath))
        {
            if (!line.StartsWith("| ", StringComparison.Ordinal)
                || line.StartsWith("| ID ", StringComparison.Ordinal)
                || line.StartsWith("| ---", StringComparison.Ordinal))
            {
                continue;
            }

            var cells = line.Trim().Trim('|').Split('|');
            if (cells.Length < 6)
            {
                continue;
            }

            var id = cells[0].Trim();
            if (!string.Equals(OpenQuestionIdPattern.Match(id).Value, id, StringComparison.Ordinal))
            {
                continue;
            }

            ownerById[id] = cells[4].Trim();
        }

        return ownerById;
    }

    private static IEnumerable<string> EnumerateTodoScanFiles(string repoRoot)
    {
        var stack = new Stack<string>();
        stack.Push(repoRoot);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            if (!dir.Equals(repoRoot, StringComparison.Ordinal) && IsExcludedScanPath(repoRoot, dir))
            {
                continue;
            }

            foreach (var subdir in Directory.GetDirectories(dir))
            {
                stack.Push(subdir);
            }

            foreach (var file in Directory.GetFiles(dir))
            {
                if (!TodoScanExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (IsExcludedScanPath(repoRoot, file))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static readonly string[] SelfExcludedPaths =
    [
        Path.Combine("tests", "OmpOS.ArchitectureTests", "RepoHygieneTests.cs"),
        Path.Combine("tests", "OmpOS.ArchitectureTests", "TodoMarkerScanner.cs"),
    ];

    private static bool IsExcludedScanPath(string repoRoot, string file)
    {
        var relative = Path.GetRelativePath(repoRoot, file);
        // These files name the markers on purpose (the MarkerPattern literals and the controls that
        // plant them), so they are excluded by exact path instead of relying on accidental self-exclusion.
        if (SelfExcludedPaths.Contains(relative, StringComparer.Ordinal))
        {
            return true;
        }

        var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (segments.Any(segment => TodoExcludedScanSegments.Contains(segment, StringComparer.Ordinal)))
        {
            return true;
        }

        for (var i = 0; i + 1 < segments.Length; i++)
        {
            if (segments[i].Equals("wwwroot", StringComparison.Ordinal) && segments[i + 1].Equals("lib", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnlinkedMarker(string line, Dictionary<string, string> ownerById)
    {
        if (!MarkerPattern.IsMatch(line))
        {
            return false;
        }

        return !OpenQuestionIdPattern.Matches(line).Any(match => ownerById.TryGetValue(match.Value, out var owner) && !string.IsNullOrWhiteSpace(owner));
    }

    /// <summary>
    /// Scans first-party code under <paramref name="repoRoot"/> and returns one
    /// "path:line: text" entry for each marker line that has no linked open question.
    /// </summary>
    public static List<string> FindUnlinkedMarkers(string repoRoot, Dictionary<string, string> ownerById)
    {
        var offenders = new List<string>();
        foreach (var file in EnumerateTodoScanFiles(repoRoot))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (HasUnlinkedMarker(lines[i], ownerById))
                {
                    offenders.Add($"{Path.GetRelativePath(repoRoot, file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        return offenders;
    }
}

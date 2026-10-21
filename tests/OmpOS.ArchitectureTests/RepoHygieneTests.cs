using System.Diagnostics;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Repository hygiene gates: no Docker-only tooling, and the docs skeleton
/// stays complete (the docs-skeleton test is appended by the docs task).
/// </summary>
public sealed class RepoHygieneTests
{
    private static readonly string[] AllowedDockerPhrases =
    [
        "never installs docker",
        "docker is not used",
        "docker is unsupported",
    ];

    private static readonly string[] AllowedDockerArtifactPaths = [];

    [Fact]
    public void RepoHygiene_NoDockerTooling()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var offenders = new List<string>();

        // ci.yml and compose.yaml arrive in the CI task of this plan; absent
        // files contribute no lines, present files are fully scanned.
        foreach (var relative in new[] { "bootstrap.sh", ".github/workflows/ci.yml", "deploy/compose.yaml" })
        {
            var path = Path.Combine(repoRoot, relative);
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var line in File.ReadAllLines(path))
            {
                if (ContainsDockerWord(line) && !line.Contains("docker.io/", StringComparison.OrdinalIgnoreCase) && !IsAllowedProse(line))
                {
                    offenders.Add($"{relative}: {line.Trim()}");
                }
            }
        }

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void NoDockerfileOrComposeFileNamed()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        using var git = Process.Start(new ProcessStartInfo("git")
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            ArgumentList = { "ls-files" },
        }) ?? throw new InvalidOperationException("could not start git");
        var tracked = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        git.ExitCode.ShouldBe(0, "git ls-files must succeed to enumerate tracked files");

        var offenders = tracked
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => Path.GetFileName(path).StartsWith("Dockerfile", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase))
            .Where(path => !AllowedDockerArtifactPaths.Contains(path, StringComparer.Ordinal))
            .ToList();

        offenders.ShouldBeEmpty();
    }

    [Fact]
    public void RepoHygiene_DocsSkeletonExists()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        string[] docs =
        [
            "README.md",
            "CONTRIBUTING.md",
            "docs/architecture.md",
            "docs/decisions/ADR-0001-monorepo-layout.md",
            "docs/decisions/ADR-0002-surface-technology.md",
            "docs/open-questions.md",
            "docs/runbooks/local-dev.md",
            "docs/runbooks/sandbox.md",
        ];
        foreach (var doc in docs)
        {
            var info = new FileInfo(Path.Combine(repoRoot, doc));
            info.Exists.ShouldBeTrue($"expected {doc} to exist");
            info.Length.ShouldBeGreaterThan(400, $"{doc} must exceed 400 bytes");
        }

        var questions = File.ReadAllLines(Path.Combine(repoRoot, "docs/open-questions.md"));
        questions.Any(static line => string.Equals(line, "| ID | Question | Why it matters | Default taken | Owner | Status |", StringComparison.Ordinal)).ShouldBeTrue("open-questions.md must contain the exact header row");
        var rows = questions.Where(static line => line.StartsWith("| ", StringComparison.Ordinal) && !line.StartsWith("| ID ", StringComparison.Ordinal) && !line.StartsWith("| ---", StringComparison.Ordinal)).ToList();
        rows.ShouldNotBeEmpty();
        foreach (var row in rows)
        {
            var cells = row.Trim().Trim('|').Split('|');
            cells.Length.ShouldBeGreaterThanOrEqualTo(6, $"open question row needs six columns: {row}");
            cells[^2].Trim().ShouldNotBeNullOrWhiteSpace($"open question row needs an owner: {row}");
            cells[^1].Trim().ShouldNotBeNullOrWhiteSpace($"open question row needs a status: {row}");
        }

        // The row count is deliberately not pinned: it changes whenever a question is added.
        rows.Select(static row => row.Split('|')[1].Trim()).Distinct(StringComparer.Ordinal).Count().ShouldBe(rows.Count, "open question IDs must be unique");
    }

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
    /// payloads are data), plus the vendored wwwroot/lib subtree only (wwwroot itself stays
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
        ".planning", "docs", "TestData",
    ];

    private static readonly Regex MarkerPattern = new(@"\b(?:TODO|FIXME|HACK|XXX)\b", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex OpenQuestionIdPattern = new(@"(?<![A-Za-z0-9-])(?:OQ-[0-9]+|Q-[A-Z][A-Z0-9-]*)(?![A-Za-z0-9-])", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    [Fact]
    public void TodoMarkersLinkToOpenQuestion()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var ownerById = ParseOpenQuestionOwners(Path.Combine(repoRoot, "docs/open-questions.md"));
        ownerById.ShouldNotBeEmpty("docs/open-questions.md must define at least one open-question ID");

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

        offenders.ShouldBeEmpty("every marker comment must link its open-question ID from docs/open-questions.md:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static Dictionary<string, string> ParseOpenQuestionOwners(string questionsPath)
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

    private static bool IsExcludedScanPath(string repoRoot, string file)
    {
        var relative = Path.GetRelativePath(repoRoot, file);
        // This file names the markers on purpose (the MarkerPattern literals below), so it is
        // excluded by exact path instead of relying on accidental self-exclusion.
        if (relative.Equals(Path.Combine("tests", "OmpOS.ArchitectureTests", "RepoHygieneTests.cs"), StringComparison.Ordinal))
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

    private static bool ContainsDockerWord(string line)
    {
        if (line.Contains("dockerfile", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var index = line.IndexOf("docker", StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            if (IsWordBoundary(line, index) && IsWordBoundary(line, index + "docker".Length))
            {
                return true;
            }

            index = line.IndexOf("docker", index + 1, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsWordBoundary(string line, int index)
    {
        return index <= 0 || index >= line.Length || (!char.IsAsciiLetterOrDigit(line[index]) && line[index] != '_');
    }

    private static bool IsAllowedProse(string line)
    {
        return AllowedDockerPhrases.Any(phrase => line.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }
}

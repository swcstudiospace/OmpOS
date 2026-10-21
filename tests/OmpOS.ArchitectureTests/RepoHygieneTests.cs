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

    private static readonly Regex MarkerPattern = new(@"\b(?:TODO|FIXME|HACK|XXX)\b", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly Regex OpenQuestionIdPattern = new(@"(?:OQ-[0-9]+|Q-[A-Z][A-Z0-9-]*)", RegexOptions.Compiled, TimeSpan.FromSeconds(2));

    private static readonly string[] ExcludedScanSegments = ["bin", "obj", "node_modules", "wwwroot", "lib", "TestData"];

    [Fact]
    public void TodoMarkersLinkToOpenQuestion()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var questionsText = File.ReadAllText(Path.Combine(repoRoot, "docs/open-questions.md"));
        var knownIds = OpenQuestionIdPattern.Matches(questionsText).Select(static match => match.Value).ToHashSet(StringComparer.Ordinal);
        knownIds.ShouldNotBeEmpty("docs/open-questions.md must define at least one open-question ID");

        var offenders = new List<string>();
        foreach (var root in new[] { "src", "services", "apps", "tests" })
        {
            var scanRoot = Path.Combine(repoRoot, root);
            if (!Directory.Exists(scanRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(scanRoot, "*.razor", SearchOption.AllDirectories)))
            {
                if (IsExcludedScanPath(repoRoot, file))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (HasUnlinkedMarker(lines[i], knownIds))
                    {
                        offenders.Add($"{Path.GetRelativePath(repoRoot, file)}:{i + 1}: {lines[i].Trim()}");
                    }
                }
            }
        }

        offenders.ShouldBeEmpty("every marker comment must link its open-question ID from docs/open-questions.md:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static bool IsExcludedScanPath(string repoRoot, string file)
    {
        var relative = Path.GetRelativePath(repoRoot, file);
        return relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => ExcludedScanSegments.Contains(segment, StringComparer.Ordinal));
    }

    private static bool HasUnlinkedMarker(string line, HashSet<string> knownIds)
    {
        var marker = MarkerPattern.Match(line);
        if (!marker.Success)
        {
            return false;
        }

        var commentStart = CommentStartIndex(line);
        if (commentStart < 0 || marker.Index < commentStart)
        {
            return false;
        }

        return !OpenQuestionIdPattern.Matches(line).Any(match => knownIds.Contains(match.Value));
    }

    private static int CommentStartIndex(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith('*')
            || trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("<!--", StringComparison.Ordinal)
            || trimmed.StartsWith("@*", StringComparison.Ordinal))
        {
            return line.Length - trimmed.Length;
        }

        var candidates = new List<int>();
        var html = line.IndexOf("<!--", StringComparison.Ordinal);
        if (html >= 0)
        {
            candidates.Add(html);
        }

        var razor = line.IndexOf("@*", StringComparison.Ordinal);
        if (razor >= 0)
        {
            candidates.Add(razor);
        }

        var block = line.IndexOf("/*", StringComparison.Ordinal);
        if (block >= 0)
        {
            candidates.Add(block);
        }

        // A double slash after a colon is a scheme separator (https://), not a comment.
        var slash = line.IndexOf("//", StringComparison.Ordinal);
        while (slash > 0 && line[slash - 1] == ':')
        {
            slash = line.IndexOf("//", slash + 2, StringComparison.Ordinal);
        }

        if (slash >= 0)
        {
            candidates.Add(slash);
        }

        return candidates.Count == 0 ? -1 : candidates.Min();
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

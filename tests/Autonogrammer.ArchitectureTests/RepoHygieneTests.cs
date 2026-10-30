using System.Diagnostics;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

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

        // A floor, not an exact count: new questions may be added, but rows must never be deleted silently (L-02).
        rows.Count.ShouldBeGreaterThanOrEqualTo(26, "docs/open-questions.md must keep at least the 26 owned rows the plan requires");
        rows.Select(static row => row.Split('|')[1].Trim()).Distinct(StringComparer.Ordinal).Count().ShouldBe(rows.Count, "open question IDs must be unique");
    }

    [Fact]
    public void TodoMarkersLinkToOpenQuestion()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var ownerById = TodoMarkerScanner.ParseOpenQuestionOwners(Path.Combine(repoRoot, "docs/open-questions.md"));
        ownerById.ShouldNotBeEmpty("docs/open-questions.md must define at least one open-question ID");

        var offenders = TodoMarkerScanner.FindUnlinkedMarkers(repoRoot, ownerById);
        offenders.ShouldBeEmpty("every marker comment must link its open-question ID from docs/open-questions.md:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void TodoScan_ReportsUnlinkedMarkerInTemporaryTree()
    {
        var root = CreateTempTree();
        try
        {
            File.WriteAllText(Path.Combine(root, "Sample.cs"), "// TODO: no open question yet" + Environment.NewLine);

            var offenders = TodoMarkerScanner.FindUnlinkedMarkers(root, OwnersWithOneQuestion());

            offenders.ShouldHaveSingleItem();
            offenders[0].ShouldStartWith("Sample.cs:1:");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TodoScan_ReportsUnlinkedAxamlMarkerInTemporaryTree()
    {
        var root = CreateTempTree();
        try
        {
            File.WriteAllText(Path.Combine(root, "Sample.axaml"), "<!-- TODO: no open question yet -->" + Environment.NewLine);

            var offenders = TodoMarkerScanner.FindUnlinkedMarkers(root, OwnersWithOneQuestion());

            offenders.ShouldHaveSingleItem();
            offenders[0].ShouldStartWith("Sample.axaml:1:");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TodoScan_SkipsPluginSubmodulesButScansTheFirstPartyPlugin()
    {
        var root = CreateTempTree();
        try
        {
            foreach (var submodule in new[] { "claude-ultrathink", "agent-swarm", "agent-substrate" })
            {
                var directory = Path.Combine(root, "plugins", submodule, "src");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "third-party.ts"), "// TODO: belongs to the sibling repository" + Environment.NewLine);
            }

            var firstParty = Path.Combine(root, "plugins", "gsd-core-omp", "src");
            Directory.CreateDirectory(firstParty);
            File.WriteAllText(Path.Combine(firstParty, "index.ts"), "// TODO: first-party marker with no open question" + Environment.NewLine);

            var offenders = TodoMarkerScanner.FindUnlinkedMarkers(root, OwnersWithOneQuestion());

            offenders.ShouldHaveSingleItem();
            offenders[0].Replace('\\', '/').ShouldStartWith("plugins/gsd-core-omp/src/index.ts:1:");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TodoScan_AcceptsLinkedMarkerInTemporaryTree()
    {
        var root = CreateTempTree();
        try
        {
            File.WriteAllText(Path.Combine(root, "Sample.cs"), "// TODO: tracked by OQ-01" + Environment.NewLine);

            TodoMarkerScanner.FindUnlinkedMarkers(root, OwnersWithOneQuestion()).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempTree()
    {
        var root = Path.Combine(Path.GetTempPath(), "autonogrammer-todo-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static Dictionary<string, string> OwnersWithOneQuestion()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal) { ["OQ-01"] = "Platform owner" };
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

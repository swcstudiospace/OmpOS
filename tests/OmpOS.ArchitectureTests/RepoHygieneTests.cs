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

    private static bool ContainsDockerWord(string line)
    {
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

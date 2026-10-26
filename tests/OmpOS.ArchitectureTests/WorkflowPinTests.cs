using Shouldly;
using Xunit;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Workflow pin gates (phase 25.4, D10): every action in every workflow is pinned
/// to a full 40-character commit SHA, and no job runs on a floating `-latest` runner.
/// </summary>
public sealed class WorkflowPinTests
{
    [Fact]
    public void WorkflowUsesLines_ArePinnedToFullCommitSha()
    {
        var files = WorkflowFiles();
        files.ShouldNotBeEmpty("expected workflow files under .github/workflows");

        var usesCount = 0;
        var offenders = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var reference = UsesReference(lines[index]);
                if (reference is null)
                {
                    continue;
                }

                usesCount++;
                if (!IsPinnedToFullSha(reference))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        usesCount.ShouldBeGreaterThan(0, "expected at least one uses: line in .github/workflows");
        offenders.ShouldBeEmpty("every uses: line must pin a full 40-hex commit SHA before any # comment");
    }

    [Fact]
    public void WorkflowRunsOn_NeverUsesLatestRunnerImage()
    {
        var files = WorkflowFiles();
        files.ShouldNotBeEmpty("expected workflow files under .github/workflows");

        var runsOnCount = 0;
        var offenders = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                var value = RunsOnValue(lines[index]);
                if (value is null)
                {
                    continue;
                }

                runsOnCount++;
                if (value.EndsWith("-latest", StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        runsOnCount.ShouldBeGreaterThan(0, "expected at least one runs-on: line in .github/workflows");
        offenders.ShouldBeEmpty("no runs-on value may end with -latest; pin the runner image (for example ubuntu-24.04)");
    }

    /// <summary>The reference of a <c>uses:</c> step line, or null when the line is not one.</summary>
    private static string? UsesReference(string line) => ValueAfterKey(line, "uses:");

    /// <summary>The value of a <c>runs-on:</c> line, or null when the line is not one.</summary>
    private static string? RunsOnValue(string line)
    {
        var value = ValueAfterKey(line, "runs-on:");
        return value?.Trim('"', '\'');
    }

    private static string? ValueAfterKey(string line, string key)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("- ", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..].TrimStart();
        }

        return trimmed.StartsWith(key, StringComparison.Ordinal)
            ? FirstToken(trimmed[key.Length..])
            : null;
    }

    private static string FirstToken(string text)
    {
        var value = text.TrimStart();
        var end = value.IndexOfAny([' ', '\t', '#']);
        return end < 0 ? value.TrimEnd() : value[..end];
    }

    private static bool IsPinnedToFullSha(string reference)
    {
        var at = reference.LastIndexOf('@');
        if (at < 0)
        {
            return false;
        }

        var sha = reference[(at + 1)..];
        return sha.Length == 40 && sha.All(static c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static string[] WorkflowFiles()
    {
        var workflows = Path.Combine(ProjectGraphChecker.FindRepoRoot(), ".github", "workflows");
        return Directory.Exists(workflows)
            ? Directory.GetFiles(workflows, "*.yml").Order(StringComparer.Ordinal).ToArray()
            : [];
    }
}

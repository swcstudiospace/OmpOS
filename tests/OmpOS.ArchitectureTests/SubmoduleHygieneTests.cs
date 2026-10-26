using System.Diagnostics;
using Shouldly;
using Xunit;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Plugin submodule gates (phase 44, AL-01): the sibling projects are git submodules under plugins/, referenced by an https
/// remote URL and never by a local path, and every submodule path in .gitmodules has a pinned commit in the index.
/// The tests read git metadata only, so they pass on a checkout whose submodules are not initialised.
/// </summary>
public sealed class SubmoduleHygieneTests
{
    private static readonly string[] ExpectedSubmodulePaths =
    [
        "plugins/claude-ultrathink",
        "plugins/agent-swarm",
        "plugins/agent-substrate",
    ];

    [Fact]
    public void GitModules_NamesTheThreePluginSubmodulesByHttpsRemoteUrl()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var entries = ParseGitModules(Path.Combine(repoRoot, ".gitmodules"));

        entries.Select(static entry => entry.Path).Order(StringComparer.Ordinal)
            .ShouldBe(ExpectedSubmodulePaths.Order(StringComparer.Ordinal), "the plugin submodules are exactly the three sibling projects");

        foreach (var entry in entries)
        {
            entry.Url.ShouldStartWith("https://github.com/", Case.Sensitive, $"{entry.Path} must use an https GitHub remote URL");
            Uri.TryCreate(entry.Url, UriKind.Absolute, out var uri).ShouldBeTrue($"{entry.Path} url must be absolute");
            uri!.UserInfo.ShouldBeEmpty($"{entry.Path} url must not embed credentials");
            entry.Url.ShouldNotContain("/root/", Case.Insensitive, $"{entry.Path} url must not name a machine-local path");
            entry.Url.ShouldNotContain("/home/", Case.Insensitive, $"{entry.Path} url must not name a machine-local path");
            entry.Url.ShouldNotContain("file:", Case.Insensitive, $"{entry.Path} url must not use the file scheme");
        }
    }

    [Fact]
    public void EveryPluginSubmodulePathIsAPinnedGitlink()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var gitlinks = RunGit(repoRoot, "ls-files", "-s", "plugins")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(static line => line.StartsWith("160000 ", StringComparison.Ordinal))
            .Select(static line => line.Split('\t', 2)[1].Trim())
            .Order(StringComparer.Ordinal)
            .ToList();

        gitlinks.ShouldBe(ExpectedSubmodulePaths.Order(StringComparer.Ordinal).ToList(), "each submodule is pinned by a gitlink in the index");
    }

    [Fact]
    public void GitModulesParser_ReadsPathAndUrlPairs()
    {
        var path = Path.Combine(Path.GetTempPath(), "ompos-gitmodules-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(
                path,
                "[submodule \"plugins/a\"]" + Environment.NewLine
                + "\tpath = plugins/a" + Environment.NewLine
                + "\turl = https://github.com/example/a.git" + Environment.NewLine
                + "\tbranch = main" + Environment.NewLine
                + "[submodule \"plugins/b\"]" + Environment.NewLine
                + "\tpath = plugins/b" + Environment.NewLine
                + "\turl = ../b" + Environment.NewLine);

            var entries = ParseGitModules(path);

            entries.Count.ShouldBe(2);
            entries[0].ShouldBe(("plugins/a", "https://github.com/example/a.git"));
            entries[1].ShouldBe(("plugins/b", "../b"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static List<(string Path, string Url)> ParseGitModules(string gitModulesPath)
    {
        File.Exists(gitModulesPath).ShouldBeTrue($"{gitModulesPath} must exist");
        var entries = new List<(string Path, string Url)>();
        string? path = null;
        string? url = null;
        foreach (var raw in File.ReadAllLines(gitModulesPath))
        {
            var line = raw.Trim();
            if (line.StartsWith("[submodule ", StringComparison.Ordinal))
            {
                if (path is not null && url is not null)
                {
                    entries.Add((path, url));
                }

                path = null;
                url = null;
            }
            else if (line.StartsWith("path", StringComparison.Ordinal) && line.Contains('=', StringComparison.Ordinal))
            {
                path = line[(line.IndexOf('=', StringComparison.Ordinal) + 1)..].Trim();
            }
            else if (line.StartsWith("url", StringComparison.Ordinal) && line.Contains('=', StringComparison.Ordinal))
            {
                url = line[(line.IndexOf('=', StringComparison.Ordinal) + 1)..].Trim();
            }
        }

        if (path is not null && url is not null)
        {
            entries.Add((path, url));
        }

        return entries;
    }

    private static string RunGit(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var git = Process.Start(startInfo) ?? throw new InvalidOperationException("could not start git");
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        git.ExitCode.ShouldBe(0, "git " + string.Join(' ', arguments) + " must succeed");
        return output;
    }
}

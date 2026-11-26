using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Pins the Phase 81 retirement (ADR-0038 decision 4): no production project references the deleted Herdr
/// harness, and no production file contains a terminal-session creation site. Both rules are read from the tree;
/// nothing about the retired engine is hard-coded except the creation-site tokens.
/// </summary>
public sealed class TerminalRetirementArchitectureTests
{
    private const string HerdrAssembly = "Autonogrammer.Harness.Herdr";

    /// <summary>The Herdr projects are gone from disk, so nothing can reference them.</summary>
    [Fact]
    public void HerdrProjectsDoNotExistOnDisk()
    {
        var root = ProjectGraphChecker.FindRepoRoot();

        Directory.Exists(Path.Combine(root, "src", HerdrAssembly)).ShouldBeFalse("the Herdr harness was deleted in Phase 81");
        Directory.Exists(Path.Combine(root, "tests", HerdrAssembly + ".Tests")).ShouldBeFalse("the Herdr tests were deleted in Phase 81");
    }

    /// <summary>No product project references the Herdr assembly.</summary>
    [Fact]
    public void NoProductionProjectReferencesHerdr()
    {
        var root = ProjectGraphChecker.FindRepoRoot();

        ProjectGraphChecker.ReadEdges(root).Where(edge => string.Equals(edge.To, HerdrAssembly, StringComparison.Ordinal)).ShouldBeEmpty();
    }

    /// <summary>The scanner reports a planted creation site and ignores a clean file.</summary>
    [Fact]
    public void SelfTestCatchesAPlantedCreationSite()
    {
        var root = Directory.CreateTempSubdirectory("terminal-retirement-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            File.WriteAllText(Path.Combine(root, "src", "Bad.cs"), "var x = host.CreateTerminalSessionAsync();");
            File.WriteAllText(Path.Combine(root, "src", "Clean.cs"), "record Kind == SessionKind.Terminal;");
            File.WriteAllText(Path.Combine(root, "src", "Bad2.cs"), "registry.Create(id, SessionKind.Terminal);");

            var hits = ScanCreationSites(root);

            // hits are "relative/path:line:"; compare the file name before the line suffix.
            hits.Count.ShouldBe(2);
            hits.ShouldContain(path => string.Equals(Path.GetFileName(path.Split(':')[0]), "Bad.cs", StringComparison.Ordinal));
            hits.ShouldContain(path => string.Equals(Path.GetFileName(path.Split(':')[0]), "Bad2.cs", StringComparison.Ordinal));
            hits.ShouldNotContain(path => string.Equals(Path.GetFileName(path.Split(':')[0]), "Clean.cs", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>No production file in the tree contains a terminal creation site.</summary>
    [Fact]
    public void FullTreeScanPasses()
    {
        ScanCreationSites(ProjectGraphChecker.FindRepoRoot()).ShouldBeEmpty();
    }

    private static List<string> ScanCreationSites(string root)
    {
        var hits = new List<string>();
        foreach (var top in new[] { "src", "services", "apps" })
        {
            var directory = Path.Combine(root, top);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (var index = 0; index < lines.Length; index++)
                {
                    if (IsCreationSite(lines[index]))
                    {
                        hits.Add(relative + ":" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ":");
                    }
                }
            }
        }

        return hits;
    }

    private static bool IsCreationSite(string line) =>
        line.Contains("CreateTerminalSessionAsync", StringComparison.Ordinal)
        || line.Contains("NewTerminalCommand", StringComparison.Ordinal)
        || (line.Contains(".Create(", StringComparison.Ordinal) && line.Contains("SessionKind.Terminal", StringComparison.Ordinal));
}

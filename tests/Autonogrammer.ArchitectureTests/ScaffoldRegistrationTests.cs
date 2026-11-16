using System.Reflection;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Guards the v0.16.0 scaffold registration: LayerMap row count, the six new
/// same-layer tuples, the process-spawning assembly set, and slnx/slnf completeness.
/// </summary>
public sealed class ScaffoldRegistrationTests
{
    private static readonly (string From, string To)[] NewTuples =
    [
        ("Autonogrammer.Accounts", "Autonogrammer.Harness.Secrets"),
        ("Autonogrammer.Harness.Secrets.Os", "Autonogrammer.Harness.Secrets"),
        ("Autonogrammer.Cowork", "Autonogrammer.Harness.AgentLoop"),
        ("Autonogrammer.Cowork.Artifacts.Stores", "Autonogrammer.Cowork.Artifacts"),
        ("Autonogrammer.UI", "Autonogrammer.Cowork.Artifacts"),
        ("Autonogrammer.Cowork.Memory.Postgres", "Autonogrammer.Cowork.Memory"),
    ];

    private static readonly string[] ExpectedProcessSpawningAssemblies =
    [
        "Autonogrammer.Harness.Acp",
        "Autonogrammer.Harness.Rpc",
        "Autonogrammer.Sandbox",
        "Autonogrammer.Harness.BrowserUse",
        "Autonogrammer.Harness.ComputerUse.MacOS",
        "Autonogrammer.Harness.Isolation",
        "Autonogrammer.Harness.Notifications",
    ];

    [Fact]
    public void LayerMapHas63LayersAndSixNewTuples()
    {
        LayerMap.Layers.Count.ShouldBe(63);
        LayerMap.SameLayerAllowed.Count.ShouldBe(29);

        foreach (var tuple in NewTuples)
        {
            LayerMap.SameLayerAllowed.ShouldContain(tuple);
        }
    }

    [Fact]
    public void ProcessSpawningAssembliesGrewToSeven()
    {
        var field = typeof(ProjectGraphTests).GetField("ProcessSpawningAssemblies", BindingFlags.NonPublic | BindingFlags.Static);
        field.ShouldNotBeNull();

        var actual = (string[])field.GetValue(null)!;
        actual.Order(StringComparer.Ordinal).ToArray().ShouldBe(ExpectedProcessSpawningAssemblies.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void EveryOnDiskProjectIsRegisteredInSlnxAndSlnf()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var slnx = File.ReadAllText(Path.Combine(repoRoot, "Autonogrammer.slnx"));
        var slnf = File.ReadAllText(Path.Combine(repoRoot, "Autonogrammer.Linux.slnf"));

        var missing = new List<string>();
        foreach (var root in new[] { "src", "services", "apps", "tests" })
        {
            foreach (var csproj in Directory.EnumerateFiles(Path.Combine(repoRoot, root), "*.csproj", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(repoRoot, csproj).Replace(Path.DirectorySeparatorChar, '/');
                if (!slnx.Contains(relative, StringComparison.Ordinal))
                {
                    missing.Add($"slnx missing {relative}");
                }

                var isAndroid = string.Equals(relative, "apps/Autonogrammer.Android/Autonogrammer.Android.csproj", StringComparison.Ordinal);
                if (!isAndroid && !slnf.Contains(relative, StringComparison.Ordinal))
                {
                    missing.Add($"slnf missing {relative}");
                }
            }
        }

        missing.ShouldBeEmpty();
    }
}

using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Asserts the on-disk .csproj reference graph respects the D-02 layers.
/// These tests catch edges ArchUnitNET cannot see (unused ProjectReferences).
/// </summary>
public sealed class ProjectGraphTests
{
    private static readonly string[] ProcessSpawningAssemblies = ["Autonogrammer.Harness.Acp", "Autonogrammer.Harness.Rpc", "Autonogrammer.Sandbox"];

    [Fact]
    public void ProjectGraph_RejectsUpwardReference()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var edges = ProjectGraphChecker.ReadEdges(repoRoot);
        ProjectGraphChecker.FindViolations(edges).ShouldBeEmpty();

        // The map must stay in sync with the tree: every LayerMap entry has a csproj
        // on disk now that the MAUI head and the EngineHost library landed in Phase 4.
        var onDisk = ProjectGraphChecker.ReadProjectNames(repoRoot);
        LayerMap.Layers.Keys.Except(onDisk, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray().ShouldBe([]);
    }

    [Fact]
    public void ProjectGraph_RejectsApiReferenceFromNonTest()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var edges = ProjectGraphChecker.ReadEdges(repoRoot);
        edges.Where(static edge => string.Equals(edge.To, "Autonogrammer.Api", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    public void ProjectGraph_WebNeverReferencesProcessSpawningAssemblies()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var edges = ProjectGraphChecker.ReadEdges(repoRoot);
        edges.Where(static edge => string.Equals(edge.From, "Autonogrammer.Web", StringComparison.Ordinal) && ProcessSpawningAssemblies.Contains(edge.To)).ShouldBeEmpty();
    }

    [Fact]
    public void ProjectGraph_RejectsSyntheticUpwardEdge()
    {
        // Live-gate proof: the checker must fire on an in-memory violation.
        var violations = ProjectGraphChecker.FindViolations([("Autonogrammer.Contracts", "Autonogrammer.Api")]);
        violations.Count.ShouldBe(1);
        violations[0].ShouldContain("Autonogrammer.Contracts");
        violations[0].ShouldContain("Autonogrammer.Api");
    }
}

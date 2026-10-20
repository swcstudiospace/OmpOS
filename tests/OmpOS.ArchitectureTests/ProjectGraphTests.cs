using Shouldly;
using Xunit;

namespace OmpOS.ArchitectureTests;

/// <summary>
/// Asserts the on-disk .csproj reference graph respects the D-02 layers.
/// These tests catch edges ArchUnitNET cannot see (unused ProjectReferences).
/// </summary>
public sealed class ProjectGraphTests
{
    private static readonly string[] ProcessSpawningAssemblies = ["OmpOS.Harness.Acp", "OmpOS.Harness.Rpc", "OmpOS.Sandbox"];

    [Fact]
    public void ProjectGraph_RejectsUpwardReference()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var edges = ProjectGraphChecker.ReadEdges(repoRoot);
        ProjectGraphChecker.FindViolations(edges).ShouldBeEmpty();

        // The map must stay in sync with the tree: only OmpOS.App may lack a
        // csproj on disk (the MAUI head arrives in Phase 4).
        var onDisk = ProjectGraphChecker.ReadProjectNames(repoRoot);
        LayerMap.Layers.Keys.Except(onDisk, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray().ShouldBe(["OmpOS.App"]);
    }

    [Fact]
    public void ProjectGraph_RejectsApiReferenceFromNonTest()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var edges = ProjectGraphChecker.ReadEdges(repoRoot);
        edges.Where(static edge => string.Equals(edge.To, "OmpOS.Api", StringComparison.Ordinal)).ShouldBeEmpty();
    }

    [Fact]
    public void ProjectGraph_WebNeverReferencesProcessSpawningAssemblies()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var edges = ProjectGraphChecker.ReadEdges(repoRoot);
        edges.Where(static edge => string.Equals(edge.From, "OmpOS.Web", StringComparison.Ordinal) && ProcessSpawningAssemblies.Contains(edge.To)).ShouldBeEmpty();
    }

    [Fact]
    public void ProjectGraph_RejectsSyntheticUpwardEdge()
    {
        // Live-gate proof: the checker must fire on an in-memory violation.
        var violations = ProjectGraphChecker.FindViolations([("OmpOS.Contracts", "OmpOS.Api")]);
        violations.Count.ShouldBe(1);
        violations[0].ShouldContain("OmpOS.Contracts");
        violations[0].ShouldContain("OmpOS.Api");
    }
}

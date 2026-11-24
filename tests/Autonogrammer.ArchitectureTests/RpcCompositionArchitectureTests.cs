using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Phase 80 freeze: <c>Autonogrammer.Harness.Rpc</c> is composed at exactly the three local
/// production roots (EngineHost, Api, Native), and the remote boundary stays on ACP until
/// Phase 83. Test projects are excluded by <see cref="ProjectGraphChecker.ReadEdges"/> and
/// may reference RPC freely (scripted parity suite, host tests).
/// </summary>
public sealed class RpcCompositionArchitectureTests
{
    /// <summary>Product project edges name Harness.Rpc only from the three local composition roots.</summary>
    [Fact]
    public void HarnessRpc_ProductionConsumersAreExactlyEngineHostApiAndNative()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var consumers = ProjectGraphChecker.ReadEdges(repoRoot)
            .Where(static edge => string.Equals(edge.To, "Autonogrammer.Harness.Rpc", StringComparison.Ordinal))
            .Select(static edge => edge.From)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        consumers.ShouldBe(["Autonogrammer.Api", "Autonogrammer.EngineHost", "Autonogrammer.Native"]);
    }

    /// <summary>Remote boundary (Phase 83) still references ACP and never RPC.</summary>
    [Theory]
    [InlineData("Autonogrammer.Harness.Remote")]
    [InlineData("Autonogrammer.AgentWorker")]
    public void RemoteBoundary_ReferencesAcpOnlyNotRpc(string project)
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var targets = ProjectGraphChecker.ReadEdges(repoRoot)
            .Where(edge => string.Equals(edge.From, project, StringComparison.Ordinal))
            .Select(static edge => edge.To)
            .ToArray();
        targets.ShouldContain("Autonogrammer.Harness.Acp", StringComparer.Ordinal);
        targets.ShouldNotContain("Autonogrammer.Harness.Rpc", StringComparer.Ordinal);
    }
}

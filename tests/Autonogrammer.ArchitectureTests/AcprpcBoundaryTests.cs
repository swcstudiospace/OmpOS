using System.Text;
using Shouldly;
using Xunit;

namespace Autonogrammer.ArchitectureTests;

/// <summary>
/// Phase 83 boundary (RPC-83-1, RPC-83-2, ADR-0038 D2): ACP is the remote boundary and RPC
/// stays local-process only, and <c>DirectSessionRunner</c> consumes
/// <c>IHarnessSessionFactory</c>/<c>IOmpSession</c> only. The edge facts come from the
/// on-disk .csproj graph via <see cref="ProjectGraphChecker"/> — never from prose. The
/// frozen RPC consumer set (EngineHost, Api, Native) is asserted by
/// <see cref="RpcCompositionArchitectureTests"/>; this class must not widen it.
/// </summary>
public sealed class AcprpcBoundaryTests
{
    /// <summary>Harness.Remote references the ACP client project and never the RPC project (ADR-0038 D2).</summary>
    [Fact]
    public void Remote_ReferencesAcpOnlyNotRpc()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var targets = ProjectGraphChecker.ReadEdges(repoRoot)
            .Where(static edge => string.Equals(edge.From, "Autonogrammer.Harness.Remote", StringComparison.Ordinal))
            .Select(static edge => edge.To)
            .ToArray();
        targets.ShouldContain("Autonogrammer.Harness.Acp", StringComparer.Ordinal);
        targets.ShouldNotContain("Autonogrammer.Harness.Rpc", StringComparer.Ordinal);
    }

    /// <summary>
    /// No RPC-over-network: no product project whose name carries a remote-transport token
    /// (Remote, Ssh, Podman, Wsl, WebSocket) references the RPC project. There is no verified
    /// remote RPC endpoint, so any such edge would be an invented relay.
    /// </summary>
    [Fact]
    public void NoRemoteNamedProject_ReferencesRpc()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var edges = ProjectGraphChecker.ReadEdges(repoRoot);
        string[] remoteTokens = ["Remote", "Ssh", "Podman", "Wsl", "WebSocket"];
        var offenders = edges
            .Where(edge => string.Equals(edge.To, "Autonogrammer.Harness.Rpc", StringComparison.Ordinal)
                && remoteTokens.Any(token => edge.From.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Select(static edge => edge.From)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        offenders.ShouldBeEmpty();
    }

    /// <summary>
    /// The frozen RPC consumer set is unchanged: exactly EngineHost, Api and Native reference
    /// Harness.Rpc among product projects. Mirrors the Phase 80 freeze so a diff here fails
    /// loudly instead of silently widening composition.
    /// </summary>
    [Fact]
    public void HarnessRpc_ConsumerSetIsFrozen()
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

    /// <summary>
    /// LayerMap keeps both harness projects in layer 2 and neither gains an upward edge
    /// (no edge to a layer-3+ project). Same-layer edges between layer-2 projects stay
    /// governed by <see cref="LayerMap.SameLayerAllowed"/>.
    /// </summary>
    [Theory]
    [InlineData("Autonogrammer.Harness.Rpc")]
    [InlineData("Autonogrammer.Harness.Remote")]
    public void HarnessProjects_StayLayer2WithNoUpwardEdge(string project)
    {
        LayerMap.Layers[project].ShouldBe(2);
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        var upward = ProjectGraphChecker.ReadEdges(repoRoot)
            .Where(edge => string.Equals(edge.From, project, StringComparison.Ordinal)
                && LayerMap.Layers.TryGetValue(edge.To, out var toLayer) && toLayer > 2)
            .Select(static edge => edge.To)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        upward.ShouldBeEmpty();
    }

    /// <summary>
    /// RPC-83-2: <c>DirectSessionRunner</c> consumes <c>IHarnessSessionFactory</c>/<c>IOmpSession</c> only and names
    /// no <c>Acp*</c> concrete type or <c>Autonogrammer.Harness.Acp</c> using. The scan tokenises the source and
    /// rejects an identifier starting with <c>Acp</c>; prose mentioning ACP as a boundary word is not a token and
    /// cannot trip it, and a concrete reference cannot hide inside a longer identifier.
    /// </summary>
    [Fact]
    public void DirectSessionRunner_NamesNoAcpConcreteType()
    {
        var repoRoot = ProjectGraphChecker.FindRepoRoot();
        string path = Path.Combine(repoRoot, "src", "Autonogrammer.Workflows", "DirectSessionRunner.cs");
        string source = File.ReadAllText(path);
        source.Contains("Autonogrammer.Harness.Acp", StringComparison.Ordinal).ShouldBeFalse();
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (char ch in source)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                current.Append(ch);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        tokens.Where(static token => token.StartsWith("Acp", StringComparison.Ordinal)).ShouldBeEmpty();
    }
}

using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Emitted YAML lists carry element values, not CLR type names (WR-08).
/// </summary>
public sealed class OpenShellPolicyListRenderingTests
{
    /// <summary>
    /// String path lists render one dash item per value (WR-08).
    /// </summary>
    [Fact]
    public void ReadOnlyPaths_RenderOneDashItemPerValue()
    {
        var policy = new OpenShellPolicy
        {
            FilesystemPolicy = new FilesystemPolicy { ReadOnly = ["/opt/a", "/opt/b"] },
        };

        var yaml = OpenShellProvider.GeneratePolicyYaml(policy);

        yaml.ShouldContain("\n  - /opt/a\n  - /opt/b\n");
        yaml.ShouldNotContain("System.String");
    }

    /// <summary>
    /// Each network rule renders as its own YAML flow mapping item, in order (WR-08).
    /// </summary>
    [Fact]
    public void NetworkRules_RenderEachAsOwnFlowMappingItem()
    {
        var policy = new OpenShellPolicy
        {
            NetworkPolicies =
            [
                new NetworkPolicyRule { Destination = "a.example.com", Port = "443", Protocol = "tcp", Action = "allow" },
                new NetworkPolicyRule { Destination = "b.example.net", Port = "80", Protocol = "tcp", Action = "deny" },
            ],
        };

        var yaml = OpenShellProvider.GeneratePolicyYaml(policy);

        yaml.ShouldContain(
            "network_policies: \n"
            + "  - {destination: a.example.com, port: 443, protocol: tcp, action: allow}\n"
            + "  - {destination: b.example.net, port: 80, protocol: tcp, action: deny}");
        yaml.ShouldNotContain("OmpOS.Sandbox.NetworkPolicyRule");
    }

    /// <summary>
    /// An empty network policy list renders as an inline empty sequence (WR-08).
    /// </summary>
    [Fact]
    public void EmptyNetworkPolicies_RenderAsInlineEmptySequence()
    {
        var yaml = OpenShellProvider.GeneratePolicyYaml(new OpenShellPolicy());

        yaml.ShouldContain("network_policies: []");
    }
}

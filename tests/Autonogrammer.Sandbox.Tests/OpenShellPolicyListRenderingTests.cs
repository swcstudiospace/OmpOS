using System.Text.Json;
using System.Text.Json.Serialization;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Emitted YAML lists carry element values, not CLR type names (WR-08).
/// </summary>
public sealed partial class OpenShellPolicyListRenderingTests
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
        yaml.ShouldNotContain("Autonogrammer.Sandbox.NetworkPolicyRule");
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

    /// <summary>
    /// Policy collections round-trip through JSON and repopulate every setter (TD-31).
    /// </summary>
    [Fact]
    public void PolicyCollections_RoundTripThroughJson_RenderIdentically()
    {
        var original = new OpenShellPolicy
        {
            FilesystemPolicy = new FilesystemPolicy
            {
                ReadOnly = ["/opt/a", "/opt/b"],
                ReadWrite = ["/var/lib/autonogrammer"],
            },
            NetworkPolicies =
            [
                new NetworkPolicyRule { Destination = "a.example.com", Port = "443", Protocol = "tcp", Action = "allow" },
            ],
        };

        var json = JsonSerializer.Serialize(original, PolicyJsonContext.Default.OpenShellPolicy);
        var restored = JsonSerializer.Deserialize(json, PolicyJsonContext.Default.OpenShellPolicy)
            ?? throw new InvalidOperationException("Serialized policy did not deserialize.");

        restored.FilesystemPolicy.ReadOnly.Count.ShouldBe(2);
        restored.FilesystemPolicy.ReadOnly[1].ShouldBe("/opt/b");
        restored.FilesystemPolicy.ReadWrite.Count.ShouldBe(1);
        restored.FilesystemPolicy.ReadWrite[0].ShouldBe("/var/lib/autonogrammer");
        restored.NetworkPolicies.Count.ShouldBe(1);
        restored.NetworkPolicies[0].Destination.ShouldBe("a.example.com");
        OpenShellProvider.GeneratePolicyYaml(restored).ShouldBe(OpenShellProvider.GeneratePolicyYaml(original));
    }

    /// <summary>
    /// A document without policy collections leaves every collection empty (TD-31 defaults).
    /// </summary>
    [Fact]
    public void PolicyCollections_AbsentFromJson_StayEmpty()
    {
        var restored = JsonSerializer.Deserialize("{}", PolicyJsonContext.Default.OpenShellPolicy)
            ?? throw new InvalidOperationException("Empty policy JSON did not deserialize.");

        restored.FilesystemPolicy.ReadOnly.ShouldBeEmpty();
        restored.FilesystemPolicy.ReadWrite.ShouldBeEmpty();
        restored.NetworkPolicies.ShouldBeEmpty();
        OpenShellProvider.GeneratePolicyYaml(restored).ShouldBe(OpenShellProvider.GeneratePolicyYaml());
    }

    /// <summary>
    /// YAML-special characters in every interpolated value are double-quoted with escapes (TD-31).
    /// </summary>
    [Fact]
    public void YamlSpecialCharacters_InEveryInterpolatedValue_AreQuotedAndEscaped()
    {
        const string special = "a:b#c{d}e[f],g\"h\ni";
        const string quoted = "\"a:b#c{d}e[f],g\\\"h\\ni\"";
        var policy = new OpenShellPolicy
        {
            FilesystemPolicy = new FilesystemPolicy { ReadOnly = [special], ReadWrite = [special] },
            Landlock = new LandlockConfig { Compatibility = special },
            NetworkPolicies =
            [
                new NetworkPolicyRule { Destination = special, Port = special, Protocol = special, Action = special },
            ],
        };

        var yaml = OpenShellProvider.GeneratePolicyYaml(policy);

        yaml.ShouldBe(
            "version: 1\n"
            + "filesystem_policy:\n"
            + "  include_workdir: true\n"
            + "  read_only: \n  - " + quoted + "\n"
            + "  read_write: \n  - " + quoted + "\n"
            + "landlock:\n"
            + "  compatibility: " + quoted + "\n"
            + "network_policies: \n  - {destination: " + quoted + ", port: " + quoted + ", protocol: " + quoted + ", action: " + quoted + "}");
        yaml.ShouldNotContain(special);
    }

    /// <summary>
    /// JSON metadata for the policy model; reflection-based serialization is disabled for this build.
    /// </summary>
    [JsonSerializable(typeof(OpenShellPolicy))]
    private sealed partial class PolicyJsonContext : JsonSerializerContext
    {
    }
}

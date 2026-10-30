using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>
/// Regression tests for non-empty network policy YAML rendering (WR-08).
/// </summary>
public sealed class PolicyYamlRegressionTests
{
    /// <summary>
    /// Non-empty network policies render as YAML mappings, not CLR type names (WR-08).
    /// </summary>
    [Fact]
    public void NonEmptyNetworkPolicies_RenderAsYamlMappings()
    {
        var policy = new OpenShellPolicy
        {
            NetworkPolicies =
            [
                new NetworkPolicyRule { Destination = "example.com", Port = "443", Protocol = "tcp", Action = "allow" },
            ],
        };

        var yaml = OpenShellProvider.GeneratePolicyYaml(policy);

        yaml.ShouldContain("- {destination: example.com, port: 443, protocol: tcp, action: allow}");
        yaml.ShouldNotContain("NetworkPolicyRule");
    }

    /// <summary>
    /// Rule values with YAML-significant characters are quoted (WR-08).
    /// </summary>
    [Fact]
    public void RuleToString_QuotesYamlSignificantValues()
    {
        var rule = new NetworkPolicyRule { Destination = "evil: value", Port = "443", Protocol = "tcp", Action = "allow" };

        var rendered = rule.ToString();

        rendered.ShouldContain("\"evil: value\"");
        rendered.ShouldContain("port: 443");
    }
}

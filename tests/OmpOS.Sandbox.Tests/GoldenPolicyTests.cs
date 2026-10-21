using System.Text;
using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Golden tests for OpenShell policy and gateway configurations.
/// Byte-exact comparison against checked-in golden files.
/// </summary>
public sealed class GoldenPolicyTests
{
    private readonly string _goldenDir;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoldenPolicyTests"/> class.
    /// </summary>
    public GoldenPolicyTests()
    {
        _goldenDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "golden");
    }

    [Fact]
    public void OpenShellProvider_GatewayToml_MatchesGolden()
    {
        var generated = OpenShellProvider.GenerateGatewayToml();
        var goldenPath = Path.Combine(_goldenDir, "openshell-gateway.toml");

        var golden = File.ReadAllText(goldenPath, Encoding.UTF8);

        Normalize(generated).ShouldBe(Normalize(golden), "Gateway TOML does not match golden file");
    }

    [Fact]
    public void OpenShellProvider_PolicyYaml_MatchesGolden()
    {
        var generated = OpenShellProvider.GeneratePolicyYaml();
        var goldenPath = Path.Combine(_goldenDir, "openshell-policy.yaml");

        var golden = File.ReadAllText(goldenPath, Encoding.UTF8);

        Normalize(generated).ShouldBe(Normalize(golden), "Policy YAML does not match golden file");
    }

    [Fact]
    public void OpenShellPolicyModel_Version1_HardRequirementLandlock()
    {
        var policy = new OpenShellPolicy();

        policy.Version.ShouldBe(1);
        policy.Landlock.Compatibility.ShouldBe("hard_requirement");
        policy.NetworkPolicies.ShouldBeEmpty();
    }

    [Fact]
    public void OpenShellGatewayModel_Version2_PodmanDriver()
    {
        var gateway = new OpenShellGatewayConfig();

        gateway.Version.ShouldBe(2);
        gateway.Gateway.ComputeDriver.ShouldBe("podman");
        gateway.Drivers.Podman.SocketPath.ShouldBe("/run/user/1000/podman/podman.sock");
    }

    private static string Normalize(string input)
    {
        return input.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();
    }
}

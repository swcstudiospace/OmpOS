using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>The gateway configuration refuses a Podman image that is unset, floats on the latest tag, or is malformed.</summary>
public sealed class OpenShellImageConfigurationTests
{
    private const string Digest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

    [Fact]
    public void AnUnsetImageIsRefusedAndTheMessageNamesTheKey()
    {
        var error = Should.Throw<ArgumentException>(() =>
        {
            OpenShellProvider.GenerateGatewayToml(new OpenShellGatewayConfig());
        });

        error.Message.ShouldContain("sandbox_runtime_image");
    }

    [Fact]
    public void ALatestImageIsRefused()
    {
        var config = ConfiguredWith(sandboxRuntime: "example.invalid/openshell-sandbox-runtime:latest");

        var error = Should.Throw<ArgumentException>(() =>
        {
            OpenShellProvider.GenerateGatewayToml(config);
        });

        error.Message.ShouldContain("sandbox_runtime_image");
    }

    [Theory]
    [InlineData("example.invalid/openshell-sandbox-runtime")]
    [InlineData("localhost:5000/openshell-sandbox-runtime")]
    [InlineData("example.invalid/openshell-sandbox-runtime@sha256:")]
    [InlineData("example.invalid/openshell-sandbox-runtime@sha256:latest")]
    [InlineData("example.invalid/openshell-sandbox-runtime:test\"\nx = \"y")]
    public void AnImageThatIsUnpinnedOrMalformedIsRefused(string image)
    {
        var config = ConfiguredWith(sandboxRuntime: image);

        var error = Should.Throw<ArgumentException>(() =>
        {
            OpenShellProvider.GenerateGatewayToml(config);
        });

        error.Message.ShouldContain("sandbox_runtime_image");
    }

    [Fact]
    public void ATaggedImageIsAccepted()
    {
        var config = ConfiguredWith(sandboxRuntime: "example.invalid/openshell-sandbox-runtime:test-1");

        OpenShellProvider.GenerateGatewayToml(config).ShouldContain("sandbox_runtime_image = \"example.invalid/openshell-sandbox-runtime:test-1\"");
    }

    [Fact]
    public void ADigestPinnedImageIsAccepted()
    {
        var image = "example.invalid/openshell-sandbox-runtime@" + Digest;
        var config = ConfiguredWith(sandboxRuntime: image);

        OpenShellProvider.GenerateGatewayToml(config).ShouldContain($"sandbox_runtime_image = \"{image}\"");
    }

    private static OpenShellGatewayConfig ConfiguredWith(string sandboxRuntime)
    {
        var config = new OpenShellGatewayConfig();
        config.Drivers.Podman.SandboxRuntimeImage = sandboxRuntime;
        config.Drivers.Podman.SupervisorImage = "example.invalid/openshell-supervisor:test-1";
        return config;
    }
}

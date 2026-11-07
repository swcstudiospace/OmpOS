using Shouldly;
using Xunit;

namespace Autonogrammer.Sandbox.Tests;

/// <summary>The gateway configuration refuses a Podman image that is unset or floats on the latest tag.</summary>
public sealed class OpenShellImageConfigurationTests
{
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

    [Fact]
    public void ATaggedImageIsAccepted()
    {
        var config = ConfiguredWith(sandboxRuntime: "example.invalid/openshell-sandbox-runtime:test-1");

        OpenShellProvider.GenerateGatewayToml(config).ShouldContain("sandbox_runtime_image = \"example.invalid/openshell-sandbox-runtime:test-1\"");
    }

    private static OpenShellGatewayConfig ConfiguredWith(string sandboxRuntime)
    {
        var config = new OpenShellGatewayConfig();
        config.Drivers.Podman.SandboxRuntimeImage = sandboxRuntime;
        config.Drivers.Podman.SupervisorImage = "example.invalid/openshell-supervisor:test-1";
        return config;
    }
}

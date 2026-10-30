using System.Net;
using Autonogrammer.Api.Harness;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Harness;

/// <summary>
/// A Production host with the ACP adapter starts only with an engine working directory of its own and an allow-listed environment. The host
/// registers <c>AddEngineIsolation</c> itself through the test services, so the tests hold whether or not the application's own wiring has it.
/// </summary>
public sealed class EngineIsolationStartupTests : IDisposable
{
    private readonly string _workspace = Path.Combine(Path.GetTempPath(), "autonogrammer-isolation-startup-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    /// <summary>A Production host without Harness:Acp:WorkspaceRoot and without worktrees fails to start and names the key.</summary>
    [Fact]
    public void ProductionWithoutAWorkspaceRootFailsStartupNamingTheKey()
    {
        var config = JwtTestHost.ProductionConfig();
        config.Remove("Harness:Acp:WorkspaceRoot");

        StartupFailureOf(config).ShouldContain("Harness:Acp:WorkspaceRoot");
    }

    /// <summary>A Production host that asks for Inherit without the unsafe opt-in fails to start and names both keys.</summary>
    [Fact]
    public void ProductionInheritWithoutTheOptInFailsStartup()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Harness:Acp:WorkspaceRoot"] = _workspace;
        config["Harness:Acp:Environment:Mode"] = "Inherit";

        var failure = StartupFailureOf(config);

        failure.ShouldContain("Harness:Acp:Environment:Mode");
        failure.ShouldContain("Harness:Acp:AllowInheritedEnvironment");
    }

    /// <summary>A relative workspace root fails to start and names the key.</summary>
    [Fact]
    public void ProductionRelativeWorkspaceRootFailsStartup()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Harness:Acp:WorkspaceRoot"] = "relative/workspaces";

        StartupFailureOf(config).ShouldContain("Harness:Acp:WorkspaceRoot must be an absolute path");
    }

    /// <summary>A Production host with a workspace root starts and answers liveness.</summary>
    [Fact]
    public async Task ProductionWithAWorkspaceRootStarts()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Harness:Acp:WorkspaceRoot"] = _workspace;
        using var factory = new ApiFactory(config, RegisterIsolation, environment: "Production");
        using var client = factory.CreateClient();

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static void RegisterIsolation(IServiceCollection services) => services.AddEngineIsolation(TestHostEnvironment.Production);

    private static string StartupFailureOf(Dictionary<string, string?> config)
    {
        using var factory = new ApiFactory(config, RegisterIsolation, environment: "Production");

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        return StartupFailureText.Of(caught);
    }
}

using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>Harness:Adapter selection: a Production host refuses the in-memory adapter and starts on the ACP adapter.</summary>
public sealed class HarnessAdapterSelectionTests
{
    /// <summary>Verifies a Production host configured with the in-memory adapter fails startup, and the failure names Harness:Adapter.</summary>
    [Fact]
    public void ProductionWithInMemoryAdapterFailsStartupNamingTheKey()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Harness:Adapter"] = "InMemory";
        using var factory = new ApiFactory(config, environment: "Production");

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Harness:Adapter");
    }

    /// <summary>Verifies a Production host configured with the ACP adapter starts and answers liveness.</summary>
    [Fact]
    public async Task ProductionWithAcpAdapterStartsAndIsLive()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Harness:Adapter"] = "Acp";
        using var factory = new ApiFactory(config, environment: "Production");
        using var client = factory.CreateClient();

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

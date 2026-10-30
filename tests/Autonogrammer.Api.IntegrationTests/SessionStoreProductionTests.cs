using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Sessions:Store startup: a Production host refuses to start on the in-memory session store and names the key, while a Development
/// host still starts on it.
/// </summary>
public sealed class SessionStoreProductionTests
{
    /// <summary>Verifies a Production host configured with the in-memory session store fails startup, and the failure names Sessions:Store.</summary>
    [Fact]
    public void ProductionWithInMemorySessionStoreFailsStartupNamingTheKey()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Sessions:Store"] = "InMemory";
        using var factory = new ApiFactory(config, environment: "Production");

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Sessions:Store");
    }

    /// <summary>Verifies a Development host configured with the in-memory session store starts and answers liveness.</summary>
    [Fact]
    public async Task DevelopmentWithInMemorySessionStoreStartsAndIsLive()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Sessions:Store"] = "InMemory" });
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

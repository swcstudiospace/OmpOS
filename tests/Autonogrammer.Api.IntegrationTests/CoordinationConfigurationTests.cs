using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Verifies the session and coordination stores fail startup, naming the missing key, when the selected store lacks its
/// connection setting, and that a Production host with the in-memory coordination store starts and says so in its log.
/// </summary>
public sealed class CoordinationConfigurationTests
{
    /// <summary>Verifies Coordination:Store=Dragonfly without a connection string fails startup naming its connection key.</summary>
    [Fact]
    public void DragonflyStoreWithoutConnectionString_FailsStartupNamingKey()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Coordination:Store"] = "Dragonfly" });

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Coordination:Dragonfly:ConnectionString");
    }

    /// <summary>Verifies Sessions:Store=Postgres without a connection string fails startup naming its connection key.</summary>
    [Fact]
    public void PostgresStoreWithoutConnectionString_FailsStartupNamingKey()
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Sessions:Store"] = "Postgres" });

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Sessions:Postgres:ConnectionString");
    }

    /// <summary>Verifies a Production host with the PostgreSQL session store and the in-memory coordination store starts, logs a warning naming Coordination:Store, and logs no connection string.</summary>
    [Fact]
    public async Task ProductionWithInMemoryCoordination_StartsAndLogsWarningNamingCoordinationKey()
    {
        var logs = new CapturingLoggerProvider();
        var config = JwtTestHost.ProductionConfig();
        config["Coordination:Store"] = "InMemory";
        using var factory = new ApiFactory(config, loggerProvider: logs, environment: "Production");
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
        live.StatusCode.ShouldBe(HttpStatusCode.OK);

        logs.Messages.Any(static message => message.Contains("Warning", StringComparison.Ordinal) && message.Contains("Coordination:Store", StringComparison.Ordinal))
            .ShouldBeTrue("at least one logged warning names Coordination:Store");
        logs.Messages.Any(static message => ConnectionStringMarkers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .ShouldBeFalse("no logged message contains a connection string");
    }

    /// <summary>Connection-string fragments that must never appear in a logged message.</summary>
    private static readonly string[] ConnectionStringMarkers = ["Host=", "Server=", "Password=", "Username=", "Data Source=", "postgres://", "postgresql://"];
}

using System.Net;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Verifies the session and coordination stores fail startup, naming the missing key, when the selected store lacks its
/// connection setting, and that a host outside Development with explicit in-process stores starts and says so in its log.
/// </summary>
public sealed class CoordinationConfigurationTests
{
    /// <summary>Verifies Coordination:Store=Dragonfly without a connection string fails startup naming its connection key.</summary>
    [Fact]
    public void DragonflyStoreWithoutConnectionString_FailsStartupNamingKey()
    {
        using var factory = new OmpApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Coordination:Store"] = "Dragonfly" });

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
        using var factory = new OmpApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { ["Sessions:Store"] = "Postgres" });

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain("Sessions:Postgres:ConnectionString");
    }

    /// <summary>Verifies a host outside Development with both stores set to InMemory starts, logs one warning naming both store keys, and logs no connection string.</summary>
    [Fact]
    public async Task ProductionWithInMemoryStores_StartsAndLogsWarningNamingBothKeys()
    {
        var logs = new CapturingLoggerProvider();
        var config = JwtTestHost.ProductionConfig();
        config["Sessions:Store"] = "InMemory";
        config["Coordination:Store"] = "InMemory";
        using var factory = new OmpApiFactory(config, loggerProvider: logs, environment: "Production");
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
        live.StatusCode.ShouldBe(HttpStatusCode.OK);

        logs.Messages.Any(static message => message.Contains("Warning", StringComparison.Ordinal) && message.Contains("Sessions:Store", StringComparison.Ordinal))
            .ShouldBeTrue("at least one logged warning names Sessions:Store");
        logs.Messages.Any(static message => message.Contains("Warning", StringComparison.Ordinal) && message.Contains("Coordination:Store", StringComparison.Ordinal))
            .ShouldBeTrue("at least one logged warning names Coordination:Store");
        logs.Messages.Any(static message => ConnectionStringMarkers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .ShouldBeFalse("no logged message contains a connection string");
    }

    /// <summary>Connection-string fragments that must never appear in a logged message.</summary>
    private static readonly string[] ConnectionStringMarkers = ["Host=", "Server=", "Password=", "Username=", "Data Source=", "postgres://", "postgresql://"];
}

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// Verifies readiness with in-process coordination: the ready endpoint answers 200 and its coordination check reports healthy.
/// </summary>
public sealed class ReadinessTests
{
    /// <summary>Verifies /health/ready is 200 and the coordination check is healthy when coordination runs in-process.</summary>
    [Fact]
    public async Task Ready_InMemoryCoordination_Returns200WithHealthyCoordinationCheck()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var report = await factory.Services.GetRequiredService<HealthCheckService>().CheckHealthAsync(testToken).ConfigureAwait(true);
        report.Entries.ContainsKey("coordination").ShouldBeTrue("the coordination check is registered for readiness");
        report.Entries["coordination"].Status.ShouldBe(HealthStatus.Healthy);
    }
}

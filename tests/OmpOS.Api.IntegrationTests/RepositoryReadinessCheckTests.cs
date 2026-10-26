using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OmpOS.Contracts.Sessions;
using OmpOS.Sessions;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// OPS-02 repository readiness: the probe reads a session id that must be absent. A store that answers that read with a session is
/// reachable but unexpected, so the repository check reports Degraded and the ready endpoint answers 503.
/// </summary>
public sealed class RepositoryReadinessCheckTests
{
    private const string ProbeSessionId = "__health__";

    /// <summary>Verifies a store that answers the absent-id probe with a session reports Degraded, and readiness answers 503 with that status.</summary>
    [Fact]
    public async Task UnexpectedProbeSession_ReportsDegradedAndReadyAnswers503()
    {
        using var factory = new OmpApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var health = factory.Services.GetRequiredService<HealthCheckService>();

        var before = await health.CheckHealthAsync(IsReady, testToken).ConfigureAwait(true);
        before.Status.ShouldBe(HealthStatus.Healthy, "an empty store answers the absent-id probe with no session");

        var repository = factory.Services.GetRequiredService<ISessionRepository>();
        await repository.SaveAsync(ProbeSession(), testToken).ConfigureAwait(true);

        var report = await health.CheckHealthAsync(IsReady, testToken).ConfigureAwait(true);
        report.Status.ShouldBe(HealthStatus.Degraded);
        report.Entries["repository"].Status.ShouldBe(HealthStatus.Degraded);

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), testToken).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(true)).ShouldBe("Degraded");
    }

    private static bool IsReady(HealthCheckRegistration registration) => registration.Tags.Contains("ready");

    private static Session ProbeSession()
    {
        var now = DateTimeOffset.UtcNow;
        return new Session
        {
            Id = ProbeSessionId,
            OwnerId = "dev",
            CreatedAt = now,
            UpdatedAt = now,
            Status = SessionStatus.Idle,
            Messages = [],
            Events = [],
        };
    }
}

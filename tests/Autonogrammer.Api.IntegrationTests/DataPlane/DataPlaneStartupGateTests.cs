using System.Net;
using Autonogrammer.Api.Hosting;
using Autonogrammer.Api.IntegrationTests.Hosting;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions;
using Autonogrammer.Sessions.Postgres;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// D-57: the startup gate probes the session store through the PostgreSQL pipeline, so while the breaker is open the probes fail fast and the first half-open probe
/// after the open window closes the breaker and opens the gate. The host never crashes. The whole run is on a manual clock: no test step sleeps.
/// </summary>
public sealed class DataPlaneStartupGateTests
{
    private static readonly TimeSpan Step = StartupGateHostedService.RetryInterval;

    /// <summary>Verifies the gate opens within one breaker open window after the store recovers, and the host stays up throughout.</summary>
    [Fact]
    public async Task Gate_OpensWithinOneOpenWindowAfterRecovery_AndTheHostNeverCrashes()
    {
        var testToken = TestContext.Current.CancellationToken;
        var time = new ManualTimeProvider();
        var options = new PostgresResilienceOptions { MaxRetries = 0 };
        var pipeline = PostgresResilience.Create(options, time).Pipeline;
        var storeUp = false;
        var repository = new PipelinedSessionRepository(
            pipeline,
            _ => storeUp ? Task.FromResult<Autonogrammer.Contracts.Sessions.Session?>(null) : throw new IOException("The store is down."));

        var app = await HostingTestHost.StartAsync(
            testToken,
            configureServices: services =>
            {
                _ = services.AddSingleton<TimeProvider>(time);
                _ = services.AddSingleton<ISessionRepository>(repository);
            }).ConfigureAwait(true);
        await using var appScope = app.ConfigureAwait(true);
        var gate = app.Services.GetRequiredService<HostingStartupGate>();
        WaitForProbe(repository, time, 1);

        // Five consecutive failures open the breaker; further probes fail fast and never reach the store.
        for (var probe = 2; probe <= 8; probe++)
        {
            time.Advance(Step);
            WaitForProbe(repository, time, probe);
        }

        pipeline.State.ShouldBe(DependencyState.Open);
        repository.Reached.ShouldBe(ResilienceOptions.BreakerConsecutiveFailures);
        gate.IsOpen.ShouldBeFalse();
        (await StatusAsync(app, testToken).ConfigureAwait(true)).ShouldBe(HttpStatusCode.ServiceUnavailable);

        // The store recovers. The breaker stays open for its window, then one probe closes it and the gate opens.
        storeUp = true;
        var recovered = time.GetUtcNow();
        var probes = repository.Completed;
        while (!gate.IsOpen && time.GetUtcNow() - recovered <= TimeSpan.FromSeconds(options.BreakerOpenSeconds) + Step)
        {
            time.Advance(Step);
            WaitForProbe(repository, time, ++probes, () => gate.IsOpen);
        }

        gate.IsOpen.ShouldBeTrue("the gate opens within one breaker open window after recovery");
        repository.Reached.ShouldBe(ResilienceOptions.BreakerConsecutiveFailures + 1, "only the half-open probe reaches the recovered store; the open breaker refused the probes before it");
        pipeline.State.ShouldBe(DependencyState.Healthy);
        (await StatusAsync(app, testToken).ConfigureAwait(true)).ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<HttpStatusCode> StatusAsync(Microsoft.AspNetCore.Builder.WebApplication app, CancellationToken cancellationToken)
    {
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(new Uri("/health/startup", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return response.StatusCode;
    }

    /// <summary>
    /// Waits, by yielding the processor and not by sleeping, until the probe with number <paramref name="probe"/> has ended and the gate loop has armed its next
    /// delay timer (or <paramref name="done"/> says the loop ended). A hard limit on real time only guards against a hung test.
    /// </summary>
    private static void WaitForProbe(PipelinedSessionRepository repository, ManualTimeProvider time, int probe, Func<bool>? done = null)
    {
        var reached = SpinWait.SpinUntil(
            () => repository.Completed >= probe && (time.PendingTimers > 0 || (done?.Invoke() ?? false)),
            TimeSpan.FromSeconds(30));
        reached.ShouldBeTrue($"probe {probe} did not end and re-arm the gate delay");
    }
}

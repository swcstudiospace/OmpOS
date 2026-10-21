using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OmpOS.Api.Coordination;
using OmpOS.Api.Workflows;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Coordination;
using OmpOS.Sessions;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace OmpOS.Api.IntegrationTests;

/// <summary>
/// Verifies the run-owner reaper. A running session whose last update is older than the stale threshold, and that no run lease
/// covers, is settled as failed with the owner-lost detail. A session a live lease covers, or one updated recently, is left running.
/// </summary>
public sealed class SessionRunReaperTests
{
    private const string OwnerLostMessage = "Prompt execution failed. Detail: run owner lost";
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Verifies a stale running session with no run lease is settled as failed with the owner-lost detail.</summary>
    [Fact]
    public async Task StaleRunningSessionWithNoLease_IsSettledFailedWithOwnerLost()
    {
        using var factory = CreateDragonflyModeFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await CreateRunningSessionAsync(factory, client, Now - TimeSpan.FromMinutes(2), testToken).ConfigureAwait(true);
        var leases = factory.Services.GetRequiredService<ISessionRunLeases>();
        (await leases.IsHeldAsync(id, testToken).ConfigureAwait(true)).ShouldBeFalse("no run owns the session");

        _ = await factory.Services.GetRequiredService<SessionRunReaper>().SweepAsync(testToken).ConfigureAwait(true);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        session.Status.ShouldBe(SessionStatus.Failed);
        session.Events[^1].Payload.ShouldBeOfType<ErrorPayload>().Message.ShouldBe(OwnerLostMessage);
    }

    /// <summary>Verifies a stale running session that a live run lease covers is left running and untouched.</summary>
    [Fact]
    public async Task StaleRunningSessionWithHeldLease_IsLeftRunning()
    {
        using var factory = CreateDragonflyModeFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await CreateRunningSessionAsync(factory, client, Now - TimeSpan.FromMinutes(2), testToken).ConfigureAwait(true);
        var leases = factory.Services.GetRequiredService<ISessionRunLeases>();
        await using var lease = (await leases.TryAcquireAsync(id, "live-owner", CoordinationDefaults.RunLeaseTimeToLive, testToken).ConfigureAwait(true))
            .ShouldNotBeNull("a live owner holds the lease").ConfigureAwait(true);

        _ = await factory.Services.GetRequiredService<SessionRunReaper>().SweepAsync(testToken).ConfigureAwait(true);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        session.Status.ShouldBe(SessionStatus.Running);
        session.Events.ShouldBeEmpty("the reaper appends nothing to a session it leaves alone");
    }

    /// <summary>Verifies a running session updated within the stale threshold is left running even with no run lease.</summary>
    [Fact]
    public async Task FreshRunningSessionWithNoLease_IsLeftRunning()
    {
        using var factory = CreateDragonflyModeFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await CreateRunningSessionAsync(factory, client, Now - TimeSpan.FromSeconds(10), testToken).ConfigureAwait(true);

        _ = await factory.Services.GetRequiredService<SessionRunReaper>().SweepAsync(testToken).ConfigureAwait(true);
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        session.Status.ShouldBe(SessionStatus.Running);
        session.Events.ShouldBeEmpty("the reaper appends nothing to a session it leaves alone");
    }

    /// <summary>
    /// Verifies the sweep does nothing when the coordination store is InMemory: a stale running session with no owner is left as it is,
    /// and the sweep reports zero settlements. The reaper is built from the host's services so the test does not depend on whether the host
    /// registers it for the InMemory store.
    /// </summary>
    [Fact]
    public async Task SweepDoesNothingWhenCoordinationIsInMemory()
    {
        using var factory = new OmpApiFactory(
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["Coordination:Store"] = "InMemory" },
            configureServices: services => services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now)));
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await CreateRunningSessionAsync(factory, client, Now - TimeSpan.FromMinutes(2), testToken).ConfigureAwait(true);
        var leases = factory.Services.GetRequiredService<ISessionRunLeases>();
        (await leases.IsHeldAsync(id, testToken).ConfigureAwait(true)).ShouldBeFalse("no run owns the session");

        using var reaper = ActivatorUtilities.CreateInstance<SessionRunReaper>(factory.Services);
        var settled = await reaper.SweepAsync(testToken).ConfigureAwait(true);

        settled.ShouldBe(0, "the sweep settles nothing when the coordination store is InMemory");
        var session = await SessionClient.ReadSessionAsync(client, id, testToken).ConfigureAwait(true);
        session.Status.ShouldBe(SessionStatus.Running);
        session.Events.ShouldBeEmpty("the sweep appends nothing");
    }

    /// <summary>Test-only placeholder that satisfies the Dragonfly connection-string validation. It is never connected to: the Dragonfly registrations are removed.</summary>
    private const string DragonflyPlaceholderConnection = "127.0.0.1:1,abortConnect=false";

    /// <summary>
    /// Creates a host in Dragonfly mode, the only mode in which the reaper sweeps. The test host has no Dragonfly server, so the Dragonfly
    /// registrations are replaced by in-process coordination; see <see cref="UseInMemoryCoordinationUnderDragonflyMode"/>.
    /// </summary>
    /// <returns>The test host factory.</returns>
    private static OmpApiFactory CreateDragonflyModeFactory() => new(
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Coordination:Store"] = "Dragonfly",
            ["Coordination:Dragonfly:ConnectionString"] = DragonflyPlaceholderConnection,
        },
        configureServices: services =>
        {
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
            UseInMemoryCoordinationUnderDragonflyMode(services);
        });

    /// <summary>
    /// Test-only. Removes the Dragonfly registrations, including the startup PING that needs a live server, and registers the in-process
    /// coordination. The store still reads as Dragonfly, so the reaper's gate opens. Its run leases are the InMemory instance the test host
    /// registers; that is valid only because the lease holder and the reaper share this one process.
    /// </summary>
    /// <param name="services">The test host service collection, after the application registered its services.</param>
    private static void UseInMemoryCoordinationUnderDragonflyMode(IServiceCollection services)
    {
        services.RemoveAll<IConnectionMultiplexer>();
        services.RemoveAll<ISessionRunLeases>();
        services.RemoveAll<ISessionSignals>();
        services.RemoveAll<IIdempotencyStore>();
        for (var i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IHostedService) && services[i].ImplementationType == typeof(CoordinationStartup))
            {
                services.RemoveAt(i);
            }
        }

        services.AddInMemoryCoordination();
    }

    /// <summary>Creates a session and stores it as running with the given last-update instant, as if its run owner had admitted it then.</summary>
    /// <param name="factory">The host whose session store holds the session.</param>
    /// <param name="client">The test client.</param>
    /// <param name="updatedAt">The last-update instant to store.</param>
    /// <param name="testToken">The test cancellation token.</param>
    /// <returns>The session identifier.</returns>
    private static async Task<string> CreateRunningSessionAsync(OmpApiFactory factory, HttpClient client, DateTimeOffset updatedAt, CancellationToken testToken)
    {
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);
        var repository = factory.Services.GetRequiredService<ISessionRepository>();
        var idle = (await repository.GetAsync(id, testToken).ConfigureAwait(true)).ShouldNotBeNull("the created session is stored");
        await repository.SaveAsync(idle with { Status = SessionStatus.Running, UpdatedAt = updatedAt }, testToken).ConfigureAwait(true);
        return id;
    }
}

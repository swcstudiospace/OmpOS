using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using Autonogrammer.Api.Audit;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.IntegrationTests.Hosting;
using Autonogrammer.Api.Options;
using Autonogrammer.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// The ledger sink is off by default, outbox-fed, and isolated from the request path. The handler is in-process; nothing dials port 7410.
/// </summary>
[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The service provider owns and disposes the handler.")]
public sealed class LedgerSinkTests
{
    /// <summary>Verifies a disabled sink writes no outbox row.</summary>
    [Fact]
    public async Task EnabledFalse_LeavesLedgerOutboxEmpty()
    {
        await using var provider = Build(enabled: false, handler: null);
        var ledger = provider.GetRequiredService<IAuditLedger>();
        ledger.ShouldBeOfType<NullAuditLedger>();
        var store = provider.GetRequiredService<ILedgerOutboxStore>();
        await ledger.RecordAsync(Sample(), transaction: null, TestContext.Current.CancellationToken);
        store.Depth.ShouldBe(0, "Enabled=false leaves ledger_outbox empty");
    }

    /// <summary>Verifies a failed emit is retried on the outbox schedule, not on the request, and a later emit is stored.</summary>
    [Fact]
    public async Task Failure_IsRetriedOnTheOutboxSchedule_AndThenStored()
    {
        var time = new ManualTimeProvider();
        var fails = 0;
        var handler = new ScriptedLedgerHandler(_ =>
        {
            fails++;
            return fails == 1
                ? Json(HttpStatusCode.ServiceUnavailable, """{"stored":false}""")
                : Json(HttpStatusCode.OK, """{"id":"evt","stored":true}""");
        });
        await using var provider = Build(enabled: true, handler, time);
        var ledger = provider.GetRequiredService<IAuditLedger>();
        var store = provider.GetRequiredService<ILedgerOutboxStore>();
        var pump = provider.GetRequiredService<LedgerOutboxPump>();
        await ledger.RecordAsync(Sample(), transaction: null, TestContext.Current.CancellationToken);
        store.Depth.ShouldBe(1);

        await pump.DrainOnceAsync(TestContext.Current.CancellationToken);
        store.Depth.ShouldBe(1, "the first failure stays in ledger_outbox");
        var early = await store.ClaimDueAsync(2, time.GetUtcNow(), TestContext.Current.CancellationToken);
        early.ShouldBeEmpty("the next attempt waits the 5 s outbox delay");

        time.Advance(TimeSpan.FromSeconds(5));
        await pump.DrainOnceAsync(TestContext.Current.CancellationToken);
        store.Depth.ShouldBe(0);
        handler.Bodies.ShouldContain(static body => body.Contains("\"kind\":\"note\"", StringComparison.Ordinal) && body.Contains("autonogrammer.audit.v1", StringComparison.Ordinal));
        foreach (var request in handler.Requests)
        {
            (request.RequestUri is null || request.RequestUri.Port != 7410).ShouldBeTrue();
        }
    }

    /// <summary>Verifies five reachability failures open the breaker and the next drain does not call the handler.</summary>
    [Fact]
    public async Task FiveFailures_OpenTheBreaker_AndTheNextDrainDoesNotEmit()
    {
        var handler = new ScriptedLedgerHandler(_ => Json(HttpStatusCode.ServiceUnavailable, """{"stored":false}"""));
        await using var provider = Build(enabled: true, handler, new ManualTimeProvider());
        var ledger = provider.GetRequiredService<IAuditLedger>();
        var pump = provider.GetRequiredService<LedgerOutboxPump>();
        for (var row = 0; row < 5; row++)
        {
            await ledger.RecordAsync(Sample(), transaction: null, TestContext.Current.CancellationToken);
        }

        await pump.DrainOnceAsync(TestContext.Current.CancellationToken);
        await pump.DrainOnceAsync(TestContext.Current.CancellationToken);
        await pump.DrainOnceAsync(TestContext.Current.CancellationToken);
        pump.State.ShouldBe(DependencyState.Open);
        var calls = handler.Requests.Count;
        calls.ShouldBe(5);
        await pump.DrainOnceAsync(TestContext.Current.CancellationToken);
        handler.Requests.Count.ShouldBe(calls, "an open breaker does not emit");
    }

    /// <summary>Verifies stopping the pump cancels an in-flight emit and returns without waiting out the ledger timeout.</summary>
    [Fact]
    public async Task Stop_CancelsAnInFlightEmit()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new ScriptedLedgerHandler(async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            }

            throw new OperationCanceledException(cancellationToken);
        });
        await using var provider = Build(enabled: true, handler, TimeProvider.System);
        var ledger = provider.GetRequiredService<IAuditLedger>();
        var pump = provider.GetRequiredService<LedgerOutboxPump>();
        await ledger.RecordAsync(Sample(), transaction: null, TestContext.Current.CancellationToken);
        await pump.StartAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var stop = pump.StopAsync(TestContext.Current.CancellationToken);
        var finished = await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.ShouldBe(stop, "the pump returns when the stopping token cancels the in-flight emit");
        await stop.ConfigureAwait(true);
    }

    /// <summary>
    /// Verifies a dispose that runs while the stop is still cancelling returns before the stop completes. The stop runs its cancellation callbacks on a
    /// thread-pool thread. This test's callback holds that cancellation open until the test releases it. It is registered after the emit's delay, so the
    /// newest-first order runs it first: the emit has not unwound and the loop has not exited. A dispose that waited for the stop would not return until
    /// the bound expires.
    /// </summary>
    [Fact]
    public async Task DisposeWhileTheStopIsCancelling_ReturnsPromptly()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelling = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(false);
        var holdCancellation = default(CancellationTokenRegistration);
        var handler = new ScriptedLedgerHandler(async (_, cancellationToken) =>
        {
            var delay = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            holdCancellation = cancellationToken.Register(() =>
            {
                cancelling.TrySetResult();
                release.Wait();
            });
            entered.TrySetResult();
            await delay.ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        });
        await using var provider = Build(enabled: true, handler, TimeProvider.System);
        var ledger = provider.GetRequiredService<IAuditLedger>();
        var pump = provider.GetRequiredService<LedgerOutboxPump>();
        await ledger.RecordAsync(Sample(), transaction: null, TestContext.Current.CancellationToken);
        await pump.StartAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var stop = Task.Run(() => pump.StopAsync(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        try
        {
            await cancelling.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var disposing = Task.Run(pump.Dispose, TestContext.Current.CancellationToken);
            var returned = await Task.WhenAny(disposing, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            returned.ShouldBe(disposing, "a dispose while the stop is cancelling must return before the stop completes");
        }
        finally
        {
            release.Set();
            await holdCancellation.DisposeAsync().ConfigureAwait(true);
        }

        var finished = await Task.WhenAny(stop, Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        finished.ShouldBe(stop, "the stop must complete once its cancellation is released");
        await stop.ConfigureAwait(true);
    }

    /// <summary>Verifies a host with the ledger sink left at its production default can start and be disposed twice.</summary>
    [Fact]
    public async Task HostDispose_Twice_DoesNotThrow_WhenLedgerDisabled()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddOptions<DataPlaneOptions>();
        builder.Services.AddOptions<LedgerResilienceOptions>();
        builder.Services.AddOptions<SessionsOptions>();
        _ = LedgerSinkServiceCollectionExtensions.AddLedgerSink(builder.Services);
        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        host.Services.GetRequiredService<IOptions<DataPlaneOptions>>().Value.Ledger.Enabled.ShouldBeFalse();
        host.Dispose();
        host.Dispose();
    }

    private static AuditLedgerEvent Sample() => new()
    {
        Item = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        Outcome = "accepted",
        Summary = "audit",
        TenantHash = new string('b', 32),
    };

    private static ServiceProvider Build(bool enabled, ScriptedLedgerHandler? handler, TimeProvider? time = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["DataPlane:Ledger:Enabled"] = enabled ? "true" : "false",
            ["DataPlane:Ledger:Endpoint"] = "http://127.0.0.1:9/",
            ["DataPlane:Ledger:Token"] = "test-token",
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOptions<DataPlaneOptions>().BindConfiguration(DataPlaneOptions.SectionName);
        services.AddOptions<LedgerResilienceOptions>().BindConfiguration(LedgerResilienceOptions.SectionName);
        services.AddSingleton(time ?? TimeProvider.System);
        if (handler is not null)
        {
            services.AddSingleton<HttpMessageHandler>(handler);
        }

        services.AddSingleton<ILedgerOutboxStore, MemoryLedgerOutboxStore>();
        _ = LedgerSinkServiceCollectionExtensions.AddLedgerSink(services);
        return services.BuildServiceProvider();
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class ScriptedLedgerHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;
        private readonly Lock _gate = new();
        private readonly List<HttpRequestMessage> _requests = [];
        private readonly List<string> _bodies = [];

        public ScriptedLedgerHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : this((request, _) => Task.FromResult(respond(request)))
        {
        }

        public ScriptedLedgerHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

        public IReadOnlyList<HttpRequestMessage> Requests
        {
            get
            {
                lock (_gate)
                {
                    return [.. _requests];
                }
            }
        }

        public IReadOnlyList<string> Bodies
        {
            get
            {
                lock (_gate)
                {
                    return [.. _bodies];
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                _requests.Add(request);
                _bodies.Add(body);
            }

            request.Headers.Authorization.ShouldNotBeNull();
            request.Headers.Authorization.Parameter.ShouldBe("test-token");
            return await _respond(request, cancellationToken).ConfigureAwait(false);
        }
    }
}

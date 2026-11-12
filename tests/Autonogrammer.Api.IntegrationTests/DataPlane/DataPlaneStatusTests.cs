using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.Options;
using Autonogrammer.Contracts.DataPlane;
using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions.Postgres;
using Autonogrammer.Sessions.Postgres.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Autonogrammer.Api.IntegrationTests.DataPlane;

/// <summary>
/// <c>GET /status/data-plane</c>: one row per component with only a component, a state and a closed reason; 401 without a token, 403 for a subject outside the
/// allow-list, 429 past the per-caller budget; and the aggregation of the pipelines, the schema probe, the exporter monitor and the ledger source.
/// </summary>
public sealed class DataPlaneStatusTests
{
    private static readonly TimeProvider Clock = TimeProvider.System;

    /// <summary>Verifies an in-memory development host reports four rows, all disabled, with exactly the three members.</summary>
    [Fact]
    public async Task InMemoryHost_ReportsFourDisabledRows_WithOnlyComponentStateAndReason()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var response = await client.GetAsync(new Uri("/status/data-plane", UriKind.Relative), testToken).ConfigureAwait(true);
        var body = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(true);
        var rows = JsonNode.Parse(body)!.AsArray();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        rows.Select(static row => row!["component"]!.GetValue<string>()).ToArray().ShouldBe(["Postgres", "Dragonfly", "Telemetry", "Ledger"]);
        foreach (var row in rows)
        {
            row!.AsObject().Select(static pair => pair.Key).Order(StringComparer.Ordinal).ToArray().ShouldBe(["component", "reason", "state"]);
            row["state"]!.GetValue<string>().ShouldBe("Disabled");
            row["reason"]!.GetValue<string>().ShouldBe("NotConfigured");
        }
    }

    /// <summary>Verifies a request without a token is refused with 401.</summary>
    [Fact]
    public async Task WithoutAToken_Gets401()
    {
        using var factory = new ApiFactory(JwtTestHost.DevelopmentConfig(), JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/status/data-plane", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>Verifies a subject outside the allow-list gets 403 and a listed subject is served.</summary>
    [Fact]
    public async Task SubjectOutsideTheAllowList_Gets403()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Api:Auth:AllowedSubjects:0"] = "alice";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("mallory"));
        using var refused = await client.GetAsync(new Uri("/status/data-plane", UriKind.Relative), testToken).ConfigureAwait(true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));
        using var served = await client.GetAsync(new Uri("/status/data-plane", UriKind.Relative), testToken).ConfigureAwait(true);

        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies the per-caller budget: the request after the budget is answered 429 with Retry-After.</summary>
    [Fact]
    public async Task PastTheBudget_Gets429WithRetryAfter()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Api:Auth:AllowedSubjects:0"] = "alice";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", JwtTestHost.IssueToken("alice"));

        for (var request = 0; request < DataPlaneStatusRateLimiterPolicy.RequestsPerMinute; request++)
        {
            using var allowed = await client.GetAsync(new Uri("/status/data-plane", UriKind.Relative), testToken).ConfigureAwait(true);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK, $"request {request + 1} is within the budget");
        }

        using var limited = await client.GetAsync(new Uri("/status/data-plane", UriKind.Relative), testToken).ConfigureAwait(true);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.ShouldNotBeNull();
    }

    /// <summary>Verifies the health probe of liveness stays anonymous and answers without any data-plane component.</summary>
    [Fact]
    public async Task LiveProbe_StaysAnonymous()
    {
        using var factory = new ApiFactory(JwtTestHost.DevelopmentConfig(), JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Verifies an open PostgreSQL breaker is unavailable with CircuitOpen, and a half-way failing one is degraded.</summary>
    [Fact]
    public async Task PostgresBreakerOpen_IsUnavailable_WithCircuitOpen()
    {
        var postgres = PostgresResilience.Create(new PostgresResilienceOptions { MaxRetries = 0 });
        await OpenBreakerAsync(postgres.Pipeline).ConfigureAwait(true);
        using var harness = new Harness(postgres: postgres, schema: SchemaState.Current);

        var rows = await harness.Service.GetAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        rows[0].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Postgres, DataPlaneState.Unavailable, DataPlaneReason.CircuitOpen));
    }

    /// <summary>Verifies the schema comparison: behind is unavailable, ahead is healthy with SchemaAhead, current is healthy.</summary>
    /// <param name="schemaName">The schema state the probe reports.</param>
    /// <param name="state">The expected state.</param>
    /// <param name="reason">The expected reason.</param>
    [Theory]
    [InlineData("Behind", DataPlaneState.Unavailable, DataPlaneReason.SchemaBehind)]
    [InlineData("Ahead", DataPlaneState.Healthy, DataPlaneReason.SchemaAhead)]
    [InlineData("Current", DataPlaneState.Healthy, DataPlaneReason.None)]
    public async Task PostgresSchema_IsComparedWithTheHead(string schemaName, DataPlaneState state, DataPlaneReason reason)
    {
        using var harness = new Harness(postgres: PostgresResilience.Create(new PostgresResilienceOptions()), schema: Enum.Parse<SchemaState>(schemaName));

        var rows = await harness.Service.GetAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        rows[0].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Postgres, state, reason));
    }

    /// <summary>Required membership and checksums take precedence over the highest recorded version, including a later registration of version 10.</summary>
    [Fact]
    public async Task SchemaProbe_RequiresEveryRegisteredVersionAndChecksum()
    {
        var token = TestContext.Current.CancellationToken;
        var current = MigrationRegistry.All.Where(static step => step.Version != 10).ToArray();
        var ten = (IMigrationStep)Activator.CreateInstance(
            typeof(MigrationRunner).Assembly.GetType("Autonogrammer.Sessions.Postgres.Migrations.M0010_DispatchDeviceState", throwOnError: true)!,
            nonPublic: true)!;
        var future = current.Append(ten).OrderBy(static step => step.Version).ToArray();
        var ledger = current.Where(static step => step.Version != 11).Select(Applied).Append(new AppliedMigration(12, "future", "future")).ToList();
        var probe = new SchemaHeadProbe(_ => Task.FromResult<IReadOnlyList<AppliedMigration>>(ledger.ToArray()), current, true, Clock);

        (await probe.GetAsync(true, token).ConfigureAwait(true)).ShouldBe(SchemaState.Behind);
        ledger.RemoveAll(static row => row.Version == 12);
        ledger.Add(Applied(current.Single(static step => step.Version == 11)));
        (await probe.GetAsync(true, token).ConfigureAwait(true)).ShouldBe(SchemaState.Current);
        var futureProbe = new SchemaHeadProbe(_ => Task.FromResult<IReadOnlyList<AppliedMigration>>(ledger.ToArray()), future, true, Clock);
        (await futureProbe.GetAsync(true, token).ConfigureAwait(true)).ShouldBe(SchemaState.Behind);
        ledger.Add(Applied(ten));
        (await futureProbe.GetAsync(true, token).ConfigureAwait(true)).ShouldBe(SchemaState.Current);
        ledger.Add(new AppliedMigration(12, "future", "future"));
        (await futureProbe.GetAsync(true, token).ConfigureAwait(true)).ShouldBe(SchemaState.Ahead);
        var index = ledger.FindIndex(static row => row.Version == 11);
        ledger[index] = ledger[index] with { Checksum = "wrong" };
        (await futureProbe.GetAsync(true, token).ConfigureAwait(true)).ShouldBe(SchemaState.Behind);
    }

    /// <summary>A forced refresh bypasses the bounded cache and non-PostgreSQL stores never read the ledger.</summary>
    [Fact]
    public async Task SchemaProbe_RefreshBypassesCache_AndNonPostgresDoesNotRead()
    {
        var token = TestContext.Current.CancellationToken;
        IReadOnlyList<AppliedMigration> ledger = MigrationRegistry.All.Select(Applied).ToArray();
        var reads = 0;
        Task<IReadOnlyList<AppliedMigration>> ReadAsync(CancellationToken _)
        {
            reads++;
            return Task.FromResult(ledger);
        }

        var probe = new SchemaHeadProbe(ReadAsync, MigrationRegistry.All, true, Clock);
        (await probe.GetAsync(false, token).ConfigureAwait(true)).ShouldBe(SchemaState.Current);
        ledger = [];
        (await probe.GetAsync(false, token).ConfigureAwait(true)).ShouldBe(SchemaState.Current);
        (await probe.GetAsync(true, token).ConfigureAwait(true)).ShouldBe(SchemaState.Behind);
        var disabled = new SchemaHeadProbe(ReadAsync, MigrationRegistry.All, false, Clock);
        (await disabled.GetAsync(true, token).ConfigureAwait(true)).ShouldBe(SchemaState.NotApplicable);
        reads.ShouldBe(2);
    }

    private static AppliedMigration Applied(IMigrationStep step) => new(step.Version, step.Name, MigrationRunner.ChecksumOf(step));

    /// <summary>Verifies a probe that times out is reported as unavailable with Timeout, and one that finds the breaker open as CircuitOpen.</summary>
    [Fact]
    public async Task PostgresProbeFailure_IsUnavailable_WithAClosedReason()
    {
        var testToken = TestContext.Current.CancellationToken;
        using (var timeout = new Harness(postgres: PostgresResilience.Create(new PostgresResilienceOptions()), probeFailure: new DataPlaneTimeoutException("postgres", DataPlaneTimeoutReason.Timeout)))
        {
            (await timeout.Service.GetAsync(testToken).ConfigureAwait(true))[0].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Postgres, DataPlaneState.Unavailable, DataPlaneReason.Timeout));
        }

        using var open = new Harness(postgres: PostgresResilience.Create(new PostgresResilienceOptions()), probeFailure: new DataPlaneTimeoutException("postgres", DataPlaneTimeoutReason.CircuitOpen));
        (await open.Service.GetAsync(testToken).ConfigureAwait(true))[0].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Postgres, DataPlaneState.Unavailable, DataPlaneReason.CircuitOpen));
    }

    /// <summary>Verifies the Dragonfly row follows the Dragonfly breaker, and is disabled for the in-memory store.</summary>
    [Fact]
    public async Task Dragonfly_FollowsItsBreaker_AndIsDisabledInMemory()
    {
        var testToken = TestContext.Current.CancellationToken;
        var dragonfly = DragonflyResilience.Create(new DragonflyResilienceOptions { MaxRetries = 0 });
        await OpenBreakerAsync(dragonfly.Pipeline).ConfigureAwait(true);
        using var enabled = new Harness(dragonfly: dragonfly, coordinationStore: CoordinationOptions.DragonflyStore);
        using var disabled = new Harness(dragonfly: dragonfly, coordinationStore: CoordinationOptions.InMemoryStore);

        (await enabled.Service.GetAsync(testToken).ConfigureAwait(true))[1].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Dragonfly, DataPlaneState.Unavailable, DataPlaneReason.CircuitOpen));
        (await disabled.Service.GetAsync(testToken).ConfigureAwait(true))[1].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Dragonfly, DataPlaneState.Disabled, DataPlaneReason.NotConfigured));
    }

    /// <summary>Verifies the ledger row: disabled by default, not configured without a sink, backlog from the threshold, open breaker as CircuitOpen.</summary>
    [Fact]
    public async Task Ledger_ReportsDisabled_NotConfigured_Backlog_AndOpen()
    {
        var testToken = TestContext.Current.CancellationToken;
        using (var off = new Harness())
        {
            (await off.Service.GetAsync(testToken).ConfigureAwait(true))[3].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Ledger, DataPlaneState.Disabled, DataPlaneReason.NotConfigured));
        }

        using (var noSink = new Harness(ledgerEnabled: true))
        {
            (await noSink.Service.GetAsync(testToken).ConfigureAwait(true))[3].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Ledger, DataPlaneState.Degraded, DataPlaneReason.NotConfigured));
        }

        using (var backlog = new Harness(ledger: new FakeLedger(DependencyState.Healthy, DataPlaneStatusService.LedgerBacklogThreshold)))
        {
            (await backlog.Service.GetAsync(testToken).ConfigureAwait(true))[3].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Ledger, DataPlaneState.Degraded, DataPlaneReason.LedgerBacklog));
        }

        using (var healthy = new Harness(ledger: new FakeLedger(DependencyState.Healthy, 0)))
        {
            (await healthy.Service.GetAsync(testToken).ConfigureAwait(true))[3].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Ledger, DataPlaneState.Healthy, DataPlaneReason.None));
        }

        using var open = new Harness(ledger: new FakeLedger(DependencyState.Open, 5));
        (await open.Service.GetAsync(testToken).ConfigureAwait(true))[3].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Ledger, DataPlaneState.Unavailable, DataPlaneReason.CircuitOpen));
    }

    /// <summary>Verifies the telemetry row is disabled unless the exporter is OTLP, and healthy when the OTLP exporter reports nothing wrong.</summary>
    [Fact]
    public async Task Telemetry_IsDisabledUnlessOtlp()
    {
        var testToken = TestContext.Current.CancellationToken;
        using var none = new Harness(exporter: "none");
        using var otlp = new Harness(exporter: "otlp");

        (await none.Service.GetAsync(testToken).ConfigureAwait(true))[2].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Telemetry, DataPlaneState.Disabled, DataPlaneReason.NotConfigured));
        (await otlp.Service.GetAsync(testToken).ConfigureAwait(true))[2].ShouldBe(new DataPlaneStatus(DataPlaneComponent.Telemetry, DataPlaneState.Healthy, DataPlaneReason.None));
    }

    /// <summary>Verifies the dependency-state gauge reports each component under its lower-case name, and the outbox gauge the ledger depth.</summary>
    [Fact]
    public async Task Gauges_ReportTheLastKnownState_WithoutIo()
    {
        var postgres = PostgresResilience.Create(new PostgresResilienceOptions { MaxRetries = 0 });
        await OpenBreakerAsync(postgres.Pipeline).ConfigureAwait(true);
        using var harness = new Harness(postgres: postgres, schema: SchemaState.Current, ledger: new FakeLedger(DependencyState.Healthy, 7), ledgerEnabled: true);
        var states = new Dictionary<string, int>(StringComparer.Ordinal);
        long depth = -1;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            // Other hosts of the same test process publish instruments of the same names; only the meter of this harness's factory counts.
            if (instrument.Name is "autonogrammer.dataplane.dependency.state" or "autonogrammer.ledger.outbox.depth" && ReferenceEquals(instrument.Meter.Scope, harness.MeterFactory))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<int>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (string.Equals(tag.Key, "component", StringComparison.Ordinal))
                {
                    states[(string)tag.Value!] = value;
                }
            }
        });
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => depth = value);
        listener.Start();

        listener.RecordObservableInstruments();

        states.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["dragonfly", "ledger", "postgres", "telemetry"]);
        states["postgres"].ShouldBe(2);
        states["dragonfly"].ShouldBe(3);
        states["ledger"].ShouldBe(0);
        depth.ShouldBe(7);
    }

    private static async Task OpenBreakerAsync(ResiliencePipeline pipeline)
    {
        for (var call = 0; call < ResilienceOptions.BreakerConsecutiveFailures && pipeline.State != DependencyState.Open; call++)
        {
            _ = await Should.ThrowAsync<IOException>(() => pipeline.ExecuteAsync<int>(OperationClass.Interactive, true, static _ => throw new IOException("down"), CancellationToken.None)).ConfigureAwait(false);
        }

        pipeline.State.ShouldBe(DependencyState.Open);
    }

    private sealed class FakeLedger(DependencyState state, long depth) : ILedgerStatusSource
    {
        public DependencyState State { get; } = state;

        public long OutboxDepth { get; } = depth;
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public Harness(
            PostgresResilience? postgres = null,
            DragonflyResilience? dragonfly = null,
            ILedgerStatusSource? ledger = null,
            SchemaState schema = SchemaState.Current,
            Exception? probeFailure = null,
            string coordinationStore = CoordinationOptions.InMemoryStore,
            string exporter = "none",
            bool ledgerEnabled = false)
        {
            var services = new ServiceCollection();
            _ = services.AddMetrics();
            _ = services.AddSingleton(Clock);
            if (postgres is not null)
            {
                _ = services.AddSingleton(postgres);
            }

            if (dragonfly is not null)
            {
                _ = services.AddSingleton(dragonfly);
            }

            if (ledger is not null)
            {
                _ = services.AddSingleton(ledger);
            }

            _ = services.AddSingleton(_ => new TelemetryExportMonitor(
                MsOptions.Create(new OtelOptions { Exporter = exporter }),
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
                Clock,
                NullLogger<TelemetryExportMonitor>.Instance));
            _provider = services.BuildServiceProvider();

            IReadOnlyList<AppliedMigration> applied = schema switch
            {
                SchemaState.Behind => [],
                SchemaState.Ahead => [.. MigrationRegistry.All.Select(Applied), new AppliedMigration(99, "future", "future")],
                _ => [.. MigrationRegistry.All.Select(Applied)],
            };
            var probe = new SchemaHeadProbe(
                _ => probeFailure is null ? Task.FromResult(applied) : Task.FromException<IReadOnlyList<AppliedMigration>>(probeFailure),
                MigrationRegistry.All,
                applies: postgres is not null,
                Clock);
            Service = new DataPlaneStatusService(
                MsOptions.Create(new SessionsOptions { Store = postgres is null ? SessionsOptions.InMemoryStore : SessionsOptions.PostgresStore }),
                MsOptions.Create(new CoordinationOptions { Store = coordinationStore }),
                MsOptions.Create(new DataPlaneOptions { Ledger = { Enabled = ledgerEnabled || ledger is not null } }),
                probe,
                _provider,
                _provider.GetRequiredService<IMeterFactory>());
        }

        public DataPlaneStatusService Service { get; }

        public IMeterFactory MeterFactory => _provider.GetRequiredService<IMeterFactory>();

        public void Dispose()
        {
            Service.Dispose();
            _provider.Dispose();
        }
    }
}

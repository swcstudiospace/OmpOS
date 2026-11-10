// SOTA embed: loading (schema wait/migration probe), error (data_plane_unavailable), success (cutover verified + telemetry to autonogrammer DB); roles fail-closed, no DDL for app, validation no old names, graceful no deploy mutations.

using System.Diagnostics.Metrics;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Telemetry;
using Autonogrammer.Contracts.DataPlane;
using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics.Metrics;
using Autonogrammer.Api.Options;
using Autonogrammer.Api.Telemetry;
using Autonogrammer.Contracts.DataPlane;
using Autonogrammer.Coordination.Dragonfly;
using Autonogrammer.Resilience;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Aggregates what the data plane knows about itself into one row per component: the state of the resilience pipelines, the schema against the compiled
/// head (probe cached) and the exporter monitor. It owns the observable gauges <c>autonogrammer.dataplane.dependency.state</c> (0 healthy, 1 degraded,
/// 2 unavailable, 3 disabled; tag <c>component</c>) and <c>autonogrammer.ledger.outbox.depth</c> on the <c>Autonogrammer.Api</c> meter. The gauges read the last
/// known answer and never perform I/O. It is registered as a hosted service that does nothing, so that the instruments exist from the start of the host and the
/// exporter sends them without a status request first. Rows carry the component, the state and a closed reason, and nothing a client could use to find the host.
/// </summary>
internal sealed class DataPlaneStatusService : IHostedService, IDisposable
{
    /// <summary>Gets the number of undelivered ledger rows from which the ledger is reported as backlogged.</summary>
    public const long LedgerBacklogThreshold = 100;

    private static readonly DataPlaneComponent[] Components = [DataPlaneComponent.Postgres, DataPlaneComponent.Dragonfly, DataPlaneComponent.Telemetry, DataPlaneComponent.Ledger];

    private readonly IOptions<SessionsOptions> _sessions;
    private readonly IOptions<CoordinationOptions> _coordination;
    private readonly IOptions<DataPlaneOptions> _options;
    private readonly SchemaHeadProbe _schema;
    private readonly IServiceProvider _services;
    private readonly Meter _meter;

    /// <summary>Initializes a new instance of the <see cref="DataPlaneStatusService"/> class.</summary>
    /// <param name="sessions">The sessions options: the PostgreSQL row exists only for the PostgreSQL store.</param>
    /// <param name="coordination">The coordination options: the Dragonfly row exists only for the Dragonfly store.</param>
    /// <param name="options">The data-plane options: the ledger row depends on <c>DataPlane:Ledger:Enabled</c>.</param>
    /// <param name="schema">The schema probe.</param>
    /// <param name="services">The service provider that holds the pipelines, the exporter monitor and the ledger source when they are registered.</param>
    /// <param name="meterFactory">The meter factory.</param>
    public DataPlaneStatusService(
        IOptions<SessionsOptions> sessions,
        IOptions<CoordinationOptions> coordination,
        IOptions<DataPlaneOptions> options,
        SchemaHeadProbe schema,
        IServiceProvider services,
        IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        _sessions = sessions;
        _coordination = coordination;
        _options = options;
        _schema = schema;
        _services = services;
        _meter = meterFactory.Create(ApiTelemetry.SourceName);
        _ = _meter.CreateObservableGauge(
            "autonogrammer.dataplane.dependency.state",
            ObserveStates,
            description: "State of each data-plane component: 0 healthy, 1 degraded, 2 unavailable, 3 disabled.");
        _ = _meter.CreateObservableGauge(
            "autonogrammer.ledger.outbox.depth",
            ObserveOutboxDepth,
            description: "Audit-ledger outbox rows that have not been delivered.");
    }

    /// <summary>Reads every component, probing the schema when its cached answer is old.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>One row per component, in the order of <see cref="DataPlaneComponent"/>.</returns>
    public async Task<DataPlaneStatus[]> GetAsync(CancellationToken cancellationToken)
    {
        SchemaState? schema = null;
        DataPlaneReason? failure = null;
        try
        {
            schema = await _schema.GetAsync(refresh: false, cancellationToken).ConfigureAwait(false);
        }
        catch (DataPlaneTimeoutException ex)
        {
            failure = ex.Reason == DataPlaneTimeoutReason.CircuitOpen ? DataPlaneReason.CircuitOpen : DataPlaneReason.Timeout;
        }
        catch (NpgsqlException ex)
        {
            failure = IsAuthFailure(ex) ? DataPlaneReason.AuthFailed : DataPlaneReason.Timeout;
        }

        return Components.Select(component => Row(component, schema, failure)).ToArray();
    }

    /// <summary>Reads every component from what is known without any I/O.</summary>
    /// <returns>One row per component.</returns>
    public DataPlaneStatus[] Snapshot() => Components.Select(component => Row(component, _schema.Last, null)).ToArray();

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose() => _meter.Dispose();

    private static bool IsAuthFailure(NpgsqlException exception) =>
        (exception as PostgresException ?? exception.InnerException as PostgresException) is { SqlState: "28P01" or "28000" };

    private static string Tag(DataPlaneComponent component) => component switch
    {
        DataPlaneComponent.Postgres => "postgres",
        DataPlaneComponent.Dragonfly => "dragonfly",
        DataPlaneComponent.Telemetry => "telemetry",
        _ => "ledger",
    };

    private static int Number(DataPlaneState state) => state switch
    {
        DataPlaneState.Healthy => 0,
        DataPlaneState.Degraded => 1,
        DataPlaneState.Unavailable => 2,
        _ => 3,
    };

    private static DataPlaneStatus FromPipeline(DataPlaneComponent component, DependencyState state) => state switch
    {
        DependencyState.Open => new DataPlaneStatus(component, DataPlaneState.Unavailable, DataPlaneReason.CircuitOpen),
        DependencyState.Degraded => new DataPlaneStatus(component, DataPlaneState.Degraded, DataPlaneReason.Timeout),
        _ => new DataPlaneStatus(component, DataPlaneState.Healthy, DataPlaneReason.None),
    };

    private static DataPlaneStatus Disabled(DataPlaneComponent component) => new(component, DataPlaneState.Disabled, DataPlaneReason.NotConfigured);

    private DataPlaneStatus Row(DataPlaneComponent component, SchemaState? schema, DataPlaneReason? failure) => component switch
    {
        DataPlaneComponent.Postgres => PostgresRow(schema, failure),
        DataPlaneComponent.Dragonfly => DragonflyRow(),
        DataPlaneComponent.Telemetry => TelemetryRow(),
        _ => LedgerRow(),
    };

    private DataPlaneStatus PostgresRow(SchemaState? schema, DataPlaneReason? failure)
    {
        var pipeline = _services.GetService<PostgresResilience>();
        if (!SessionsOptions.IsPostgresStore(_sessions.Value.Store) || pipeline is null)
        {
            return Disabled(DataPlaneComponent.Postgres);
        }

        var row = FromPipeline(DataPlaneComponent.Postgres, pipeline.Pipeline.State);
        if (row.State == DataPlaneState.Unavailable)
        {
            return row;
        }

        if (failure is { } reason)
        {
            return new DataPlaneStatus(DataPlaneComponent.Postgres, DataPlaneState.Unavailable, reason);
        }

        return schema switch
        {
            SchemaState.Behind => new DataPlaneStatus(DataPlaneComponent.Postgres, DataPlaneState.Unavailable, DataPlaneReason.SchemaBehind),
            SchemaState.Ahead when row.State == DataPlaneState.Healthy => new DataPlaneStatus(DataPlaneComponent.Postgres, DataPlaneState.Healthy, DataPlaneReason.SchemaAhead),
            _ => row,
        };
    }

    private DataPlaneStatus DragonflyRow()
    {
        var pipeline = _services.GetService<DragonflyResilience>();
        return !CoordinationOptions.IsDragonflyStore(_coordination.Value.Store) || pipeline is null
            ? Disabled(DataPlaneComponent.Dragonfly)
            : FromPipeline(DataPlaneComponent.Dragonfly, pipeline.State);
    }

    private DataPlaneStatus TelemetryRow()
    {
        var monitor = _services.GetService<TelemetryExportMonitor>();
        if (monitor is null || !monitor.IsExporting)
        {
            return Disabled(DataPlaneComponent.Telemetry);
        }

        return monitor.IsFailing
            ? new DataPlaneStatus(DataPlaneComponent.Telemetry, DataPlaneState.Degraded, DataPlaneReason.ExporterFailing)
            : new DataPlaneStatus(DataPlaneComponent.Telemetry, DataPlaneState.Healthy, DataPlaneReason.None);
    }

    private DataPlaneStatus LedgerRow()
    {
        if (!_options.Value.Ledger.Enabled)
        {
            return Disabled(DataPlaneComponent.Ledger);
        }

        var source = _services.GetService<ILedgerStatusSource>();
        if (source is null)
        {
            return new DataPlaneStatus(DataPlaneComponent.Ledger, DataPlaneState.Degraded, DataPlaneReason.NotConfigured);
        }

        var row = FromPipeline(DataPlaneComponent.Ledger, source.State);
        return row.State == DataPlaneState.Healthy && source.OutboxDepth >= LedgerBacklogThreshold
            ? new DataPlaneStatus(DataPlaneComponent.Ledger, DataPlaneState.Degraded, DataPlaneReason.LedgerBacklog)
            : row;
    }

    private List<Measurement<int>> ObserveStates() =>
        [.. Snapshot().Select(static row => new Measurement<int>(Number(row.State), new KeyValuePair<string, object?>("component", Tag(row.Component))))];

    private IEnumerable<Measurement<long>> ObserveOutboxDepth()
    {
        var source = _services.GetService<ILedgerStatusSource>();
        return source is null ? [] : [new Measurement<long>(source.OutboxDepth)];
    }
}

namespace Autonogrammer.Contracts.DataPlane;

/// <summary>A store or exporter of the data plane that <c>GET /status/data-plane</c> reports on.</summary>
public enum DataPlaneComponent
{
    /// <summary>The PostgreSQL (TimescaleDB) store of sessions, accounts and the outbox.</summary>
    Postgres,

    /// <summary>The Dragonfly store of leases, presence, idempotency records and rate-limit windows.</summary>
    Dragonfly,

    /// <summary>The OTLP exporter that ships traces and metrics to GreptimeDB.</summary>
    Telemetry,

    /// <summary>The audit-ledger sink.</summary>
    Ledger,
}

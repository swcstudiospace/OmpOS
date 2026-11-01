namespace Autonogrammer.Contracts.DataPlane;

/// <summary>The closed set of reasons a component is not plainly healthy. A client keys its copy on this value, never on text.</summary>
public enum DataPlaneReason
{
    /// <summary>There is nothing to report.</summary>
    None,

    /// <summary>The circuit breaker of the component is open: calls are refused without reaching it.</summary>
    CircuitOpen,

    /// <summary>A call to the component exceeded its deadline or the component is saturated.</summary>
    Timeout,

    /// <summary>The component refused its credentials.</summary>
    AuthFailed,

    /// <summary>The component is not configured in this deployment.</summary>
    NotConfigured,

    /// <summary>The schema is older than the compiled head; a migration is pending.</summary>
    SchemaBehind,

    /// <summary>The schema is newer than the compiled head; this build is older than the database. This is accepted.</summary>
    SchemaAhead,

    /// <summary>The store cannot enforce the configured retention.</summary>
    RetentionUnsupported,

    /// <summary>The exporter reports failures or its endpoint is not reachable.</summary>
    ExporterFailing,

    /// <summary>The ledger outbox holds rows that have not been delivered.</summary>
    LedgerBacklog,
}

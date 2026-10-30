namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Binds the <c>DataPlane</c> configuration section: the tenant-hash pepper, the PostgreSQL schema wait, the retention windows and the audit-ledger sink.
/// The resilience sections (<c>DataPlane:Postgres:Resilience</c>, <c>DataPlane:Dragonfly:Resilience</c>, <c>DataPlane:Ledger:Resilience</c>) are bound by their own
/// option classes in <c>Autonogrammer.Resilience</c>. Every cross-field rule is in <see cref="DataPlaneOptionsValidator"/>; its messages name keys, never values.
/// </summary>
internal sealed class DataPlaneOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "DataPlane";

    /// <summary>Gets the configuration key of the tenant-hash pepper (environment form <c>DataPlane__TenantHashPepper</c>).</summary>
    public const string TenantHashPepperKey = "DataPlane:TenantHashPepper";

    /// <summary>Gets or sets the secret key of the tenant hash. Required when <c>Coordination:Store</c> is Dragonfly; never logged.</summary>
    public string? TenantHashPepper { get; set; }

    /// <summary>Gets or sets the PostgreSQL settings.</summary>
    public DataPlanePostgresOptions Postgres { get; set; } = new();

    /// <summary>Gets or sets the retention windows.</summary>
    public DataPlaneRetentionOptions Retention { get; set; } = new();

    /// <summary>Gets or sets the audit-ledger sink.</summary>
    public DataPlaneLedgerOptions Ledger { get; set; } = new();
}

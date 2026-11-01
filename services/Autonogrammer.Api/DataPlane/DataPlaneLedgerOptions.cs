namespace Autonogrammer.Api.DataPlane;

/// <summary>
/// Binds <c>DataPlane:Ledger</c>: the audit-ledger sink. <see cref="Endpoint"/> and <see cref="Token"/> are validated only when <see cref="Enabled"/> is
/// <see langword="true"/>; the token travels in a header only and is never logged.
/// </summary>
internal sealed class DataPlaneLedgerOptions
{
    /// <summary>Gets or sets a value indicating whether audit events are sent to the ledger. The default is <see langword="false"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the absolute https endpoint (http on a loopback host) of the ledger.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Gets or sets the bearer token of the ledger.</summary>
    public string? Token { get; set; }
}

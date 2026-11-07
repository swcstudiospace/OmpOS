namespace Autonogrammer.Api.Audit;

/// <summary>
/// The only shape that may be notarised. The ledger is append-only and cannot be scrubbed, so this type is closed: a new property fails
/// <c>AuditLedgerEventFieldLockTests</c>. Kind is always <c>note</c>. There is no email, device name, command body, prompt, account id or IP;
/// the account appears only as <see cref="TenantHash"/>.
/// </summary>
internal sealed record AuditLedgerEvent
{
    /// <summary>The action every audit event carries. It is not a caller-supplied string.</summary>
    public const string ActionValue = "autonogrammer.audit.v1";

    /// <summary>The kind the ledger vocabulary accepts for this sink.</summary>
    public const string Kind = "note";

    /// <summary>Gets the action. It is always <see cref="ActionValue"/>.</summary>
    public string Action { get; init; } = ActionValue;

    /// <summary>Gets the audit item, a ULID. It is not an account id.</summary>
    public required string Item { get; init; }

    /// <summary>Gets the outcome word, for example <c>accepted</c>.</summary>
    public required string Outcome { get; init; }

    /// <summary>Gets the short fixed summary. It is never a prompt or a command body.</summary>
    public required string Summary { get; init; }

    /// <summary>Gets the keyed tenant hash of the account (32 lowercase hex characters).</summary>
    public required string TenantHash { get; init; }
}

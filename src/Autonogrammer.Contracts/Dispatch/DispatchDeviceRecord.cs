namespace Autonogrammer.Contracts.Dispatch;

/// <summary>One device row. A foreign account never receives this record.</summary>
/// <param name="AccountId">Owning account.</param>
/// <param name="DeviceId">Device identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="PublicKey">SPKI, when recorded.</param>
/// <param name="CredentialJti">Current credential identifier.</param>
/// <param name="CredentialExpiresAt">Credential expiry.</param>
/// <param name="Capabilities">Newest capability snapshot.</param>
/// <param name="PairedSessionId">Sign-in session recorded at pairing.</param>
/// <param name="LastSeenAt">Last presence touch.</param>
/// <param name="RevokedAt">Revocation time. A revoked device does not count toward the cap.</param>
/// <param name="RevokeReason">Revoke reason, when set.</param>
public sealed record DispatchDeviceRecord(
    string AccountId,
    string DeviceId,
    string Name,
    string? PublicKey,
    string? CredentialJti,
    DateTimeOffset? CredentialExpiresAt,
    IReadOnlyList<DispatchCapabilitySnapshot> Capabilities,
    string? PairedSessionId,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? RevokedAt,
    string? RevokeReason);

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Fields required to insert a device row.</summary>
/// <param name="AccountId">Owning account.</param>
/// <param name="DeviceId">Device identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="PublicKey">SPKI, when recorded.</param>
/// <param name="CredentialJti">Initial credential identifier.</param>
/// <param name="CredentialExpiresAt">Credential expiry.</param>
/// <param name="Capabilities">Capability snapshot.</param>
/// <param name="PairedSessionId">Sign-in session recorded at pairing.</param>
public sealed record CreateDispatchDevice(
    string AccountId,
    string DeviceId,
    string Name,
    string? PublicKey,
    string? CredentialJti,
    DateTimeOffset? CredentialExpiresAt,
    IReadOnlyList<DispatchCapabilitySnapshot> Capabilities,
    string? PairedSessionId);

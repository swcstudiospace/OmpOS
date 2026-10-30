

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Inputs of the command signature. Every value is restricted to <c>[A-Za-z0-9._:-]</c>.</summary>
/// <param name="Version">Protocol version text, <c>1</c>.</param>
/// <param name="CommandId">Command identifier.</param>
/// <param name="Seq">Per-device sequence, decimal.</param>
/// <param name="UserId">Account identifier.</param>
/// <param name="DeviceId">Device identifier.</param>
/// <param name="Capability">Capability wire name.</param>
/// <param name="Op">Operation wire name.</param>
/// <param name="IssuedAt">Issued-at Unix seconds, decimal.</param>
/// <param name="ExpiresAt">Expires-at Unix seconds, decimal.</param>
/// <param name="Nonce">Delivery nonce.</param>
/// <param name="PayloadSha256">Hex SHA-256 of the decoded payload.</param>
/// <param name="KeyId">Signing key identifier.</param>
public readonly record struct DispatchCanonicalInput(
    string Version,
    string CommandId,
    string Seq,
    string UserId,
    string DeviceId,
    string Capability,
    string Op,
    string IssuedAt,
    string ExpiresAt,
    string Nonce,
    string PayloadSha256,
    string KeyId);


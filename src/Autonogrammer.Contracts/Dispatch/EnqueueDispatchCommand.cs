namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Fields required to enqueue a command.</summary>
/// <param name="AccountId">Owning account.</param>
/// <param name="DeviceId">Target device.</param>
/// <param name="CommandId">Hub-generated command identifier.</param>
/// <param name="IdempotencyKey">Caller key, or null when the caller sent none.</param>
/// <param name="Fingerprint">SHA-256 of the canonical request.</param>
/// <param name="Capability">Command capability. Tool-class flags are refused by the route, not stored as operations.</param>
/// <param name="Op">Closed operation.</param>
/// <param name="BodyJson">Stored body. The signature is not stored.</param>
/// <param name="IssuedAt">Hub clock.</param>
/// <param name="ExpiresAt">Delivery deadline.</param>
public sealed record EnqueueDispatchCommand(
    string AccountId,
    string DeviceId,
    string CommandId,
    string? IdempotencyKey,
    string Fingerprint,
    DispatchCapability Capability,
    DispatchOp Op,
    string BodyJson,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

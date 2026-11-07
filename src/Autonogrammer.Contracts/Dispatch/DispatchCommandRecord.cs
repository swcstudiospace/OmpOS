namespace Autonogrammer.Contracts.Dispatch;

/// <summary>One command row.</summary>
/// <param name="AccountId">Owning account.</param>
/// <param name="DeviceId">Target device.</param>
/// <param name="CommandId">Command identifier.</param>
/// <param name="Seq">Per-device sequence, starting at 1 with no gaps at allocation.</param>
/// <param name="State">Hub state.</param>
/// <param name="Capability">Command capability.</param>
/// <param name="Op">Operation.</param>
/// <param name="Fingerprint">Request fingerprint.</param>
/// <param name="IdempotencyKey">Caller key, when one was sent.</param>
/// <param name="BodyJson">Stored body, null after retention.</param>
/// <param name="IssuedAt">Hub clock at accept.</param>
/// <param name="ExpiresAt">Delivery deadline.</param>
/// <param name="Reason">Failure reason, when the state carries one.</param>
public sealed record DispatchCommandRecord(
    string AccountId,
    string DeviceId,
    string CommandId,
    long Seq,
    DispatchCommandState State,
    DispatchCapability Capability,
    DispatchOp Op,
    string Fingerprint,
    string? IdempotencyKey,
    string? BodyJson,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DispatchFailureReason? Reason);

using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Dispatch.Desktop;

/// <summary>A verified, not yet executed command, with its decoded payload.</summary>
/// <param name="CommandId">The command identifier.</param>
/// <param name="Capability">The command capability.</param>
/// <param name="Op">The operation.</param>
/// <param name="Payload">The decoded payload: UTF-8 JSON.</param>
public sealed record DispatchCommand(string CommandId, DispatchCapability Capability, DispatchOp Op, ReadOnlyMemory<byte> Payload);

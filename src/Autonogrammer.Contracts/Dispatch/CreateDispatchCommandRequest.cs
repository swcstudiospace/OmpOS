namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Body of <c>POST /dispatch/devices/{id}/commands</c>.</summary>
public sealed record CreateDispatchCommandRequest
{
    /// <summary>Command capability. Tool-class flags are refused.</summary>
    public required DispatchCapability Capability { get; init; }

    /// <summary>Closed operation.</summary>
    public required DispatchOp Op { get; init; }

    /// <summary>Base64url payload.</summary>
    public required string Payload { get; init; }

    /// <summary>When false, an offline device is refused instead of queued.</summary>
    public bool? QueueIfOffline { get; init; }
}

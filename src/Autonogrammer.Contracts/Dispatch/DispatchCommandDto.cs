namespace Autonogrammer.Contracts.Dispatch;

/// <summary>One command row returned to its owner.</summary>
public sealed record DispatchCommandDto
{
    /// <summary>Command identifier.</summary>
    public required string CommandId { get; init; }

    /// <summary>Device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Per-device sequence.</summary>
    public required long Seq { get; init; }

    /// <summary>Hub state.</summary>
    public required DispatchCommandState State { get; init; }

    /// <summary>Command capability.</summary>
    public required DispatchCapability Capability { get; init; }

    /// <summary>Operation.</summary>
    public required DispatchOp Op { get; init; }

    /// <summary>Issued-at Unix seconds.</summary>
    public required long IssuedAt { get; init; }

    /// <summary>Expires-at Unix seconds.</summary>
    public required long ExpiresAt { get; init; }

    /// <summary>Failure reason, when the state carries one.</summary>
    public DispatchFailureReason? Reason { get; init; }

    /// <summary>Redacted terminal result text, if supplied by the device.</summary>
    public string? ResultText { get; init; }
}

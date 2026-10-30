namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Hub to device welcome.</summary>
public sealed record DispatchWelcomeFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>welcome</c>.</summary>
    public required string T { get; init; }

    /// <summary>Hub clock, Unix seconds.</summary>
    public required long ServerTime { get; init; }

    /// <summary>Heartbeat interval in seconds.</summary>
    public required int HeartbeatSeconds { get; init; }

    /// <summary>Current signing key identifiers.</summary>
    public required IReadOnlyList<string> Kids { get; init; }

    /// <summary>Renewed device credential, when the hub rotated it.</summary>
    public string? RenewedCredential { get; init; }

    /// <summary>Sequence the hub will resume delivery from.</summary>
    public required long ResumeFromSeq { get; init; }
}

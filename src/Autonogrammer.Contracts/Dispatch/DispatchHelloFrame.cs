namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Device to hub hello.</summary>
public sealed record DispatchHelloFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>hello</c>.</summary>
    public required string T { get; init; }

    /// <summary>Device signature over the hello canonical bytes.</summary>
    public required string Signature { get; init; }

    /// <summary>Challenge nonce.</summary>
    public required string Nonce { get; init; }

    /// <summary>Device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Origin the desktop believes it reached.</summary>
    public required string Origin { get; init; }

    /// <summary>Last sequence the device applied.</summary>
    public required long LastAppliedSeq { get; init; }

    /// <summary>Capability snapshot.</summary>
    public IReadOnlyList<DispatchCapabilitySnapshot>? Capabilities { get; init; }

    /// <summary>Command identifiers the device is still running, at most 32.</summary>
    public IReadOnlyList<string>? Running { get; init; }
}

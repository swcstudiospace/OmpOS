namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Body of <c>POST /dispatch/devices</c>.</summary>
public sealed record PairDispatchDeviceRequest
{
    /// <summary>Display name chosen by the user.</summary>
    public required string Name { get; init; }

    /// <summary>Device public key, SPKI base64url.</summary>
    public required string PublicKey { get; init; }

    /// <summary>Capability snapshot.</summary>
    public required IReadOnlyList<DispatchCapabilitySnapshot> Capabilities { get; init; }

    /// <summary>Desktop application version.</summary>
    public string? AppVersion { get; init; }

    /// <summary>Relay protocol the desktop speaks.</summary>
    public string? Protocol { get; init; }
}

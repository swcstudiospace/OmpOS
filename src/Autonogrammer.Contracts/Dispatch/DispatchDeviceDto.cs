namespace Autonogrammer.Contracts.Dispatch;

/// <summary>One device row returned to its owner.</summary>
public sealed record DispatchDeviceDto
{
    /// <summary>Device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>Revocation time, Unix seconds, when revoked.</summary>
    public long? RevokedAt { get; init; }

    /// <summary>Last seen time, Unix seconds.</summary>
    public long? LastSeenAt { get; init; }

    /// <summary>Newest capability snapshot.</summary>
    public required IReadOnlyList<DispatchCapabilitySnapshot> Capabilities { get; init; }

    /// <summary>Sign-in session recorded at pairing.</summary>
    public string? PairedSessionId { get; init; }

    /// <summary>Presence read through the presence seam: <c>online</c>, <c>offline</c> or <c>unknown</c>. Unknown is never offline.</summary>
    public string? Presence { get; init; }
}

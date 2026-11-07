namespace Autonogrammer.Contracts.Dispatch;

/// <summary>201 body of a successful pairing.</summary>
public sealed record PairDispatchDeviceResponse
{
    /// <summary>New device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Device credential. Shown once.</summary>
    public required string Credential { get; init; }

    /// <summary>Credential expiry, Unix seconds.</summary>
    public required long CredentialExpiresAt { get; init; }
}

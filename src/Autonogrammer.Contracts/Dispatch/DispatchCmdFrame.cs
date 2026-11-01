namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Hub to device signed command.</summary>
public sealed record DispatchCmdFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>cmd</c>.</summary>
    public required string T { get; init; }

    /// <summary>26-character command identifier.</summary>
    public required string CommandId { get; init; }

    /// <summary>Per-device sequence.</summary>
    public required long Seq { get; init; }

    /// <summary>Account identifier.</summary>
    public required string UserId { get; init; }

    /// <summary>Device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Command capability wire name.</summary>
    public required string Capability { get; init; }

    /// <summary>Operation wire name.</summary>
    public required string Op { get; init; }

    /// <summary>Issued-at Unix seconds.</summary>
    public required long IssuedAt { get; init; }

    /// <summary>Expires-at Unix seconds.</summary>
    public required long ExpiresAt { get; init; }

    /// <summary>Delivery nonce.</summary>
    public required string Nonce { get; init; }

    /// <summary>Base64url payload.</summary>
    public required string Payload { get; init; }

    /// <summary>Hex SHA-256 of the decoded payload.</summary>
    public required string PayloadSha256 { get; init; }

    /// <summary>Signing key identifier.</summary>
    public required string Kid { get; init; }

    /// <summary>Raw P-1363 ES256 signature, base64url.</summary>
    public required string Sig { get; init; }
}

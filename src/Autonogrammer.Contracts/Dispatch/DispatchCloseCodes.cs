namespace Autonogrammer.Contracts.Dispatch;

/// <summary>WebSocket close codes reserved for the Dispatch relay. No other application code is a Dispatch close.</summary>
public static class DispatchCloseCodes
{
    /// <summary>Normal closure.</summary>
    public const int Normal = 1000;

    /// <summary>The hub is shutting down. The device reconnects.</summary>
    public const int GoingAway = 1001;

    /// <summary>The frame is not one JSON object.</summary>
    public const int InvalidPayload = 1007;

    /// <summary>Policy violation, including a repeated unknown frame type.</summary>
    public const int PolicyViolation = 1008;

    /// <summary>The frame is over 1 MiB.</summary>
    public const int MessageTooBig = 1009;

    /// <summary>Internal error.</summary>
    public const int InternalError = 1011;

    /// <summary>Try again later. The device reconnects with jitter.</summary>
    public const int TryAgainLater = 1013;

    /// <summary>The device was revoked. Terminal.</summary>
    public const int DeviceRevoked = 4001;

    /// <summary>The credential or handshake proof is invalid. Terminal until re-pair.</summary>
    public const int CredentialInvalid = 4002;

    /// <summary>Replaced by a newer connection.</summary>
    public const int Replaced = 4009;

    /// <summary>Unsupported protocol version. Terminal until update.</summary>
    public const int UnsupportedVersion = 4010;

    /// <summary>The account is unavailable. Terminal.</summary>
    public const int AccountUnavailable = 4011;

    /// <summary>Rate limited. The reason carries a retry delay.</summary>
    public const int RateLimited = 4020;

    /// <summary>Reports whether <paramref name="code"/> is one of the closed Dispatch close codes.</summary>
    /// <param name="code">The close code.</param>
    /// <returns><see langword="true"/> when the code is in the closed set.</returns>
    public static bool IsKnown(int code) => code is
        Normal or GoingAway or InvalidPayload or PolicyViolation or MessageTooBig or InternalError or TryAgainLater
        or DeviceRevoked or CredentialInvalid or Replaced or UnsupportedVersion or AccountUnavailable or RateLimited;
}

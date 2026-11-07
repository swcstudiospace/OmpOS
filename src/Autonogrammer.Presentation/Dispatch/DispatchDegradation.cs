namespace Autonogrammer.Presentation.Dispatch;

/// <summary>The degraded states that are not a phase of their own. Each shows its own message; none is conveyed by colour alone.</summary>
public enum DispatchDegradation
{
    /// <summary>The presence service is down, so a device's connection is not known.</summary>
    PresenceUnknown,

    /// <summary>The sign-in for this device expired.</summary>
    CredentialExpired,

    /// <summary>The device was revoked.</summary>
    DeviceRevoked,

    /// <summary>Another copy of the app holds the connection.</summary>
    ReplacedByOtherInstance,

    /// <summary>The app is too old for this device.</summary>
    UpdateRequired,

    /// <summary>The account cannot use Dispatch right now.</summary>
    AccountUnavailable,

    /// <summary>Too many commands were sent.</summary>
    RateLimited,

    /// <summary>The command is too large to send.</summary>
    PayloadTooLarge,

    /// <summary>A capability is not available on the device.</summary>
    CapabilityUnavailable,

    /// <summary>The user paused Dispatch on this device.</summary>
    PausedByUser,

    /// <summary>Dispatch is paused for maintenance.</summary>
    CoordinationPaused,

    /// <summary>The device has no key store to protect its key.</summary>
    KeyStoreUnavailable,

    /// <summary>The device is paired to another account.</summary>
    PairedToAnotherAccount,
}

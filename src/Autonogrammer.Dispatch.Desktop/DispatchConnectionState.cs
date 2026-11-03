namespace Autonogrammer.Dispatch.Desktop;

/// <summary>What the desktop shows about its link to the hub.</summary>
public enum DispatchConnectionState
{
    /// <summary>Opening the connection.</summary>
    Connecting,

    /// <summary>Connected and accepting verified commands.</summary>
    Connected,

    /// <summary>Waiting before the next attempt.</summary>
    Reconnecting,

    /// <summary>Another desktop instance took this device's connection. The desktop waits 60 seconds and tries again.</summary>
    ReplacedByOtherInstance,

    /// <summary>The credential is no longer valid. Terminal until the device is paired again.</summary>
    CredentialExpired,

    /// <summary>The device was revoked. Terminal.</summary>
    DeviceRevoked,

    /// <summary>The hub requires a newer desktop. Terminal until the desktop is updated.</summary>
    UpdateRequired,

    /// <summary>The account is unavailable to this device. Terminal.</summary>
    AccountUnavailable,

    /// <summary>The hub reports that Dispatch is off for this account. The desktop keeps trying.</summary>
    DispatchDisabled,

    /// <summary>The device key or credential cannot be read from the secret store. The desktop does not connect.</summary>
    KeyStoreUnavailable,
}

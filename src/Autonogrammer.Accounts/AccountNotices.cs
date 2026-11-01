namespace Autonogrammer.Accounts;

/// <summary>Things the person should be told about the session. Several may hold at once.</summary>
[Flags]
public enum AccountNotices
{
    /// <summary>Nothing to tell.</summary>
    None = 0,

    /// <summary>The refresh token could not be written to the secret store; the person is signed in until the app closes.</summary>
    SessionNotRemembered = 1,

    /// <summary>The service did not confirm the sign-out; the device is signed out regardless.</summary>
    SignOutNotConfirmed = 2,

    /// <summary>A stored session belonged to another project and was discarded.</summary>
    ServiceChanged = 4,

    /// <summary>A stored session could not be refreshed any more and was cleared.</summary>
    SessionEnded = 8,

    /// <summary>
    /// The device signed the person out but the stored secret could not be removed and the service did not confirm the sign-out, so this device may
    /// sign in again at the next start; the person can remove it in the system's password settings.
    /// </summary>
    SignOutNotCleared = 16,
}

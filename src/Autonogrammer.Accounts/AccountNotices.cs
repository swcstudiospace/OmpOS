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
}

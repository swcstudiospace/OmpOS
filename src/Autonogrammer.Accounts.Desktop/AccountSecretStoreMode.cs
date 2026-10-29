namespace Autonogrammer.Accounts.Desktop;

/// <summary>Where an <see cref="AccountSecretStoreSelector"/> currently keeps the signed-in session.</summary>
public enum AccountSecretStoreMode
{
    /// <summary>No session is stored and none was written yet: the system secret store has not been asked.</summary>
    NotAsked,

    /// <summary>The system secret store (libsecret, Keychain or DPAPI) holds the session.</summary>
    OperatingSystem,

    /// <summary>The system secret store failed; the session lives in memory only and the notice <c>SessionNotRemembered</c> applies.</summary>
    MemoryOnly,

    /// <summary>The person chose to remember the sign-in without system protection: an owner-only file holds the session.</summary>
    OwnerOnlyFile,
}

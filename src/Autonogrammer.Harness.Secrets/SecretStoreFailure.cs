namespace Autonogrammer.Harness.Secrets;

/// <summary>Why a secret store could not complete an operation. The kind carries no value; callers decide from it how to degrade.</summary>
public enum SecretStoreFailure
{
    /// <summary>The backing store does not exist on this device: no service, no native library, or an unsupported platform.</summary>
    Unavailable,

    /// <summary>The backing store exists but is locked and was not unlocked.</summary>
    Locked,

    /// <summary>The backing store refused this process access, or the person declined the prompt.</summary>
    Denied,

    /// <summary>Stored data could not be read: damaged, unsupported format or not decryptable by this user.</summary>
    Corrupt,

    /// <summary>Any other failure, including every failure of a store that does not classify its errors.</summary>
    Other,
}

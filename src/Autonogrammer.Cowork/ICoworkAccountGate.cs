namespace Autonogrammer.Cowork;

/// <summary>
/// The signed-in account seam for Cowork storage. The Native head implements it. This assembly never reads an account subject.
/// </summary>
public interface ICoworkAccountGate
{
    /// <summary>Gets whether an account is signed in.</summary>
    bool IsSignedIn { get; }

    /// <summary>Gets the account folder key, or null when signed out. The key is never the account subject.</summary>
    string? AccountFolderKey { get; }
}

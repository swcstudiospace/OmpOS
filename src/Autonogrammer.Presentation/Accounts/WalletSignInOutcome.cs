namespace Autonogrammer.Presentation.Accounts;

/// <summary>How a wallet sign-in ended, as far as the panel needs to know.</summary>
public enum WalletSignInOutcome
{
    /// <summary>The wallet signed the message and the service accepted it.</summary>
    SignedIn,

    /// <summary>The person refused the request in the wallet, or the wallet returned no account or no signature.</summary>
    Declined,

    /// <summary>No wallet is present in this browser.</summary>
    Unavailable,

    /// <summary>The service refused the signature, or the request could not be completed.</summary>
    Failed,
}

namespace Autonogrammer.Contracts.Accounts;

/// <summary>
/// The one sign-in state a head draws at a time. Offline is a notice on <see cref="SignedIn"/>, never a phase of its own.
/// </summary>
public enum AuthPhase
{
    /// <summary>No session: the address field and the send-code action are shown.</summary>
    SignedOut,

    /// <summary>A code was sent: the code field, the resend cooldown and the expiry countdown are shown.</summary>
    AwaitingLink,

    /// <summary>A code or a link is being checked, or a stored session is being restored; the person may cancel.</summary>
    SigningIn,

    /// <summary>A session exists. The account menu and any notices are shown.</summary>
    SignedIn,

    /// <summary>One fixed message for an <see cref="AuthErrorKind"/> and a primary action are shown.</summary>
    Error,
}

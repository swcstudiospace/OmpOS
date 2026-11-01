using Autonogrammer.Contracts.Accounts;

namespace Autonogrammer.Accounts;

/// <summary>
/// The state a head draws. <see cref="Phase"/> is only ever <see cref="AuthPhase.SignedOut"/> or <see cref="AuthPhase.SignedIn"/> here; the heads add
/// the transient phases of a sign-in in progress.
/// </summary>
/// <param name="Phase">Signed in or signed out.</param>
/// <param name="Identity">Who is signed in, or null when signed out.</param>
/// <param name="Connectivity">Whether the session is in step with the service. Meaningful while signed in.</param>
/// <param name="Notices">What the person should be told.</param>
public sealed record AccountState(AuthPhase Phase, AccountIdentity? Identity, AccountConnectivity Connectivity, AccountNotices Notices)
{
    /// <summary>Gets the state of a person who is signed out and has nothing to be told.</summary>
    public static AccountState SignedOut { get; } = new(AuthPhase.SignedOut, null, AccountConnectivity.Online, AccountNotices.None);
}

using Autonogrammer.Contracts.Accounts;

namespace Autonogrammer.Presentation.Accounts;

/// <summary>
/// What an <see cref="IAccountHost"/> is showing. The presentation layer owns this shape so it never sees a session, a token or an Accounts type.
/// Offline is a flag on <see cref="AuthPhase.SignedIn"/>, never a phase of its own.
/// </summary>
/// <param name="Phase">The one sign-in state to draw.</param>
/// <param name="Email">The address to show, or null when none is known.</param>
/// <param name="Offline">True when a signed-in session cannot reach the service. The phase stays <see cref="AuthPhase.SignedIn"/>.</param>
/// <param name="SessionNotRemembered">True when the sign-in could not be written to the system secret store.</param>
/// <param name="SignOutNotConfirmed">True when the device signed out but the service did not confirm it.</param>
/// <param name="ServiceChanged">True when a stored session belonged to another project and was discarded.</param>
/// <param name="DevicesAvailable">True only when the account features list contains <c>dispatch</c>.</param>
/// <param name="Configured">False when the API has no client sign-in block (not configured).</param>
/// <param name="ExpiresAt">When the emailed code stops being valid, or null when no code is awaited.</param>
/// <param name="ResendAvailableAt">When another code may be requested, or null when no code is awaited.</param>
/// <param name="RemainingAttempts">How many more wrong codes are accepted before a new email must be requested.</param>
/// <param name="MustResend">True when five wrong codes have been tried, so the code field no longer submits.</param>
/// <param name="Error">The failure to show in the error state, or null otherwise.</param>
/// <param name="LoopbackUnavailable">True when the email has a code but no link can return to this device.</param>
public sealed record AccountHostState(
    AuthPhase Phase,
    string? Email,
    bool Offline,
    bool SessionNotRemembered,
    bool SignOutNotConfirmed,
    bool ServiceChanged,
    bool DevicesAvailable,
    bool Configured,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ResendAvailableAt,
    int RemainingAttempts,
    bool MustResend,
    AuthErrorKind? Error,
    bool LoopbackUnavailable)
{
    /// <summary>Gets the signed-out state of a configured host with nothing to tell.</summary>
    public static AccountHostState SignedOut { get; } = new(
        AuthPhase.SignedOut,
        null,
        false,
        false,
        false,
        false,
        false,
        true,
        null,
        null,
        0,
        false,
        null,
        false);
}

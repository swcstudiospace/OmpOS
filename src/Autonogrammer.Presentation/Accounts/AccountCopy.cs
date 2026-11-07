using Autonogrammer.Contracts.Accounts;

namespace Autonogrammer.Presentation.Accounts;

/// <summary>
/// The only sentences the account panel shows. Each <see cref="AuthPhase"/> and each <see cref="AuthErrorKind"/> has exactly one fixed sentence.
/// Nothing here interpolates a server message, an exception or a token.
/// </summary>
public static class AccountCopy
{
    /// <summary>The name of the account panel.</summary>
    public const string PanelName = "Account";

    /// <summary>The name of the email field.</summary>
    public const string EmailFieldName = "Email address";

    /// <summary>The label of the action that sends a code.</summary>
    public const string SendCodeLabel = "Send code";

    /// <summary>The name of the code field. It accepts 6 to 10 digits.</summary>
    public const string CodeFieldName = "Email code";

    /// <summary>The label of the action that requests another code.</summary>
    public const string ResendLabel = "Send another code";

    /// <summary>The label of the action that leaves the code screen.</summary>
    public const string ChangeEmailLabel = "Change email";

    /// <summary>The label of the action that abandons a sign-in in progress.</summary>
    public const string CancelLabel = "Cancel sign-in";

    /// <summary>The heading above the sign-in options that are not the email code.</summary>
    public const string OtherWaysHeading = "Other ways to sign in";

    /// <summary>The label of the button that starts a sign-in with an Ethereum wallet in this browser.</summary>
    public const string WalletButtonLabel = "Continue with an Ethereum wallet";

    /// <summary>The sentence after a provider sign-in opened its tab.</summary>
    public const string ProviderStartedSentence = "Finish signing in in the new tab. This tab updates when the sign-in completes. If no tab opened, allow pop-ups for this site.";

    /// <summary>The sentence when a provider sign-in could not start.</summary>
    public const string ProviderUnavailableSentence = "That sign-in could not start. Try again, or use the email code.";

    /// <summary>The sentence when the wallet request was refused or closed.</summary>
    public const string WalletDeclinedSentence = "The wallet request was declined. Nothing was signed.";

    /// <summary>The sentence when no wallet is available in this browser.</summary>
    public const string WalletUnavailableSentence = "No Ethereum wallet is available in this browser.";

    /// <summary>The sentence when the wallet sign-in could not be completed.</summary>
    public const string WalletFailedSentence = "The wallet sign-in could not be completed. Try again, or use the email code.";

    /// <summary>The label of the button that starts a sign-in with a provider.</summary>
    /// <param name="provider">The provider name: <c>google</c> or <c>x</c>.</param>
    /// <returns>The label.</returns>
    public static string ProviderButtonLabel(string provider) => provider switch
    {
        "google" => "Continue with Google",
        "x" => "Continue with X",
        _ => "Continue with this account",
    };

    /// <summary>The label of the action that signs out this device.</summary>
    public const string SignOutLabel = "Sign out";

    /// <summary>The label of the action that signs out every session.</summary>
    public const string SignOutEverywhereLabel = "Sign out everywhere";

    /// <summary>The label of the Devices row, shown only when the features list contains dispatch.</summary>
    public const string DevicesLabel = "Devices";

    /// <summary>The name of the identity line.</summary>
    public const string IdentityName = "Signed-in account";

    /// <summary>The name of the error message. It is a live region.</summary>
    public const string ErrorName = "Sign-in error";

    /// <summary>The label of the action that leaves the error state.</summary>
    public const string ErrorDismissLabel = "Back";

    /// <summary>The name of the expiry countdown. The visible text changes every second; the live name does not.</summary>
    public const string ExpiryName = "Code expiry";

    /// <summary>The name of the resend cooldown.</summary>
    public const string CooldownName = "Resend cooldown";

    /// <summary>The sentence shown when five wrong codes have been tried.</summary>
    public const string MustResend = "Too many wrong codes. Request another code.";

    /// <summary>The sentence shown when sign-in is not configured on this API.</summary>
    public const string NotConfigured = "Sign-in is not configured for this API.";

    /// <summary>The sentence shown when a link cannot return to this device.</summary>
    public const string LoopbackUnavailable = "A link opened on another device cannot sign you in here; use the code from the email instead.";

    /// <summary>The notice shown on a signed-in account that cannot reach the service. It is never a sign-out.</summary>
    public const string OfflineNotice = "You are offline. You stay signed in until the connection returns.";

    /// <summary>The notice shown when the sign-in could not be stored, and the sentence that offers the consent action.</summary>
    public const string SessionNotRememberedNotice = "This sign-in is not remembered on this device. It ends when the app closes.";

    /// <summary>The label of the consent action offered only beside <see cref="SessionNotRememberedNotice"/>.</summary>
    public const string RememberWithoutProtectionLabel = "Remember sign-in on this device without system protection";

    /// <summary>The notice shown when the device signed out but the service did not confirm it.</summary>
    public const string SignOutNotConfirmedNotice = "This device is signed out. The service did not confirm the sign-out.";

    /// <summary>The notice shown when a stored session belonged to another project and was discarded.</summary>
    public const string ServiceChangedNotice = "The saved sign-in belonged to a different service and was discarded.";

    /// <summary>The live announcement when the code expires in 30 seconds.</summary>
    public const string ExpiryIn30Seconds = "The code expires in 30 seconds.";

    /// <summary>The live announcement when the code expires in 10 seconds.</summary>
    public const string ExpiryIn10Seconds = "The code expires in 10 seconds.";

    /// <summary>The live announcement when another code may be requested.</summary>
    public const string ResendAvailableAnnouncement = "You can request another code.";

    /// <summary>The static signing-in text used when motion is reduced.</summary>
    public const string SigningInLabel = "Signing in";

    /// <summary>Returns the one fixed sentence for a phase.</summary>
    /// <param name="phase">The phase being drawn.</param>
    /// <returns>The sentence. It contains no server text.</returns>
    public static string For(AuthPhase phase) => phase switch
    {
        AuthPhase.SignedOut => "Sign in with the code from your email.",
        AuthPhase.AwaitingLink => "Enter the code from the email, or open the link on this device.",
        AuthPhase.SigningIn => "Checking the sign-in. You can cancel.",
        AuthPhase.SignedIn => "You are signed in.",
        AuthPhase.Error => "Sign-in could not be completed.",
        _ => "Sign-in could not be completed.",
    };

    /// <summary>Returns the one fixed sentence for a failure. The same kind always returns the same sentence.</summary>
    /// <param name="kind">The failure to explain.</param>
    /// <returns>The sentence. It contains no server text.</returns>
    public static string For(AuthErrorKind kind) => kind switch
    {
        AuthErrorKind.EmailRateLimited => "Too many emails were sent this hour. Wait and try again.",
        AuthErrorKind.TooManyRequests => "Too many requests. Wait and try again.",
        AuthErrorKind.CodeInvalidOrExpired => "That code is not valid. Check it or request another.",
        AuthErrorKind.LinkInvalidOrExpired => "That link is not valid. Use the code from the email.",
        AuthErrorKind.LinkOpenedInWrongTab => "That link opened in a different tab. Use the code from the email.",
        AuthErrorKind.EmailInvalid => "That email address is not valid.",
        AuthErrorKind.SignUpsClosed => "New sign-ups are closed.",
        AuthErrorKind.CaptchaRequired => "This project requires a check this app cannot complete.",
        AuthErrorKind.AccountBlocked => "This account is blocked.",
        AuthErrorKind.MethodUnavailable => "Email sign-in is not available.",
        AuthErrorKind.SessionEnded => "The saved sign-in ended. Sign in again.",
        AuthErrorKind.NetworkUnavailable => "The service could not be reached.",
        AuthErrorKind.ServiceUnavailable => "The service is unavailable. Try again later.",
        AuthErrorKind.ConfigUnavailable => "Sign-in is not available right now.",
        AuthErrorKind.AccountRejected => "The service did not accept this sign-in. You have been signed out on this device.",
        AuthErrorKind.Unexpected => "Something went wrong. Try again.",
        _ => "Something went wrong. Try again.",
    };

    /// <summary>Returns the visible countdown. It is not a catalogue sentence and is not announced every second.</summary>
    /// <param name="seconds">Whole seconds remaining, already rounded up.</param>
    /// <returns>The visible text.</returns>
    public static string ExpiryText(int seconds) => seconds <= 0 ? "The code has expired." : FormattableString.Invariant($"The code expires in {seconds} seconds.");

    /// <summary>Returns the visible resend wait. It is not announced every second.</summary>
    /// <param name="seconds">Whole seconds until another code may be requested.</param>
    /// <returns>The visible text.</returns>
    public static string CooldownText(int seconds) => seconds <= 0 ? ResendAvailableAnnouncement : FormattableString.Invariant($"You can request another code in {seconds} seconds.");
}

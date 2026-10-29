namespace Autonogrammer.UI.Components.Auth;

/// <summary>Every text of the sign-in screens, in one place so no component carries a literal.</summary>
public static class AuthCopy
{
    /// <summary>The product name shown while the sign-in configuration loads.</summary>
    public const string ProductName = "Autonogrammer";

    /// <summary>The document title segment of the loading screen.</summary>
    public const string LoadingTitle = "Loading";

    /// <summary>The status text while the sign-in configuration loads.</summary>
    public const string Loading = "Loading Autonogrammer…";

    /// <summary>The status text while the browser is sent to the connect page.</summary>
    public const string Redirecting = "Taking you to the sign-in page…";

    /// <summary>The document title segment of the unreachable screen.</summary>
    public const string UnavailableTitle = "Cannot reach the API";

    /// <summary>The heading of the unreachable screen.</summary>
    public const string UnavailableHeading = "Autonogrammer cannot reach its API";

    /// <summary>The alert of the unreachable screen.</summary>
    public const string UnavailableBody = "The sign-in settings of this deployment could not be loaded. Check your connection, then try again.";

    /// <summary>The label of the button that loads the sign-in settings again.</summary>
    public const string Retry = "Retry";

    /// <summary>The document title segment of the screen for a deployment that signs in with OIDC.</summary>
    public const string OidcTitle = "Sign-in not available";

    /// <summary>The heading of the screen for a deployment that signs in with OIDC.</summary>
    public const string OidcHeading = "Sign-in is not available here";

    /// <summary>The explanation for a deployment that signs in with OIDC. It is information, not an error.</summary>
    public const string OidcBody = "Sign-in for this deployment is not available in this web build. Use the desktop app, or ask the operator of this deployment for a build that supports its sign-in.";

    /// <summary>The document title segment of the connect page.</summary>
    public const string ConnectTitle = "Connect";

    /// <summary>The heading of the connect page.</summary>
    public const string ConnectHeading = "Connect to Autonogrammer";

    /// <summary>The help text under the heading.</summary>
    public const string ConnectHelp = "Enter the access token of this deployment. It stays in this browser tab only and is forgotten when you close the tab.";

    /// <summary>The visible label of the token field.</summary>
    public const string TokenLabel = "Access token";

    /// <summary>The label of the visually hidden username field that lets a password manager store the token.</summary>
    public const string UsernameLabel = "Username";

    /// <summary>The value of the visually hidden username field.</summary>
    public const string Username = "owner";

    /// <summary>The label of the submit button.</summary>
    public const string Connect = "Connect";

    /// <summary>The status text while the token is checked.</summary>
    public const string Checking = "Checking the token…";

    /// <summary>The error when the token field is empty.</summary>
    public const string TokenRequired = "Enter the access token.";

    /// <summary>The error when the API refused the token.</summary>
    public const string TokenRejected = "That token was not accepted. Check it and enter it again.";

    /// <summary>The error when the API could not be reached.</summary>
    public const string ConnectUnreachable = "Autonogrammer could not reach its API. Check your connection, then select Connect to try again.";

    /// <summary>The notice when a session ended because the API refused the token.</summary>
    public const string SessionEnded = "Your session ended. Enter the access token again to continue.";

    /// <summary>The label of the sign-out button in the top bar.</summary>
    public const string SignOut = "Sign out";
}

namespace Autonogrammer.Accounts.Desktop;

/// <summary>The names of the two account settings the desktop head stores. This is the single home of both names.</summary>
public static class AccountSettingsKeys
{
    /// <summary>The setting that is true while a signed-in session is stored. A head asks the system secret store at startup only when it is true.</summary>
    public const string HasStoredSession = "account.hasStoredSession";

    /// <summary>The setting that is true once the person chose to remember the sign-in in an owner-only file, without system protection.</summary>
    public const string AllowFileSecretStore = "account.allowFileSecretStore";
}

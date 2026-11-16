namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// Every user-facing sentence of the Cowork surface that this phase owns. Views and tests read the same constants.
/// </summary>
public static class CoworkCopy
{
    /// <summary>The signed-out sentence. The sign-in button beside it opens the sign-in window.</summary>
    public const string SignInToUseCowork = "Sign in to use Cowork";

    /// <summary>The label of the button that opens the sign-in window from the signed-out Cowork panel.</summary>
    public const string SignInButtonLabel = "Sign in";

    /// <summary>The quiet mark when a summary could not be synced.</summary>
    public const string NotSynced = "Not synced";

    /// <summary>The sentence a full Cowork list shows. It matches the registry cap message.</summary>
    public const string SessionLimit = "You have 200 Cowork tasks. Delete or archive one to start another.";

    /// <summary>The skip link that moves focus to the conversation.</summary>
    public const string SkipToConversation = "Skip to conversation";

    /// <summary>The heading of tasks that are not in a project.</summary>
    public const string NoProject = "No project";

    /// <summary>The live-region setting for hidden-surface activity. It is polite status, never assertive.</summary>
    public const string PoliteLiveSetting = "Polite";

    /// <summary>Builds the quarantine sentence. <paramref name="keptName"/> replaces the file-name placeholder.</summary>
    /// <param name="keptName">The quarantine file name. Blank becomes the word "file".</param>
    /// <returns>The sentence a head shows when the Cowork file could not be read.</returns>
    public static string TasksCouldNotBeRead(string keptName)
    {
        var name = string.IsNullOrWhiteSpace(keptName) ? "file" : keptName.Trim();
        return "Cowork tasks could not be read; the file was kept as " + name;
    }

    /// <summary>Builds the hidden-surface chip for a running count, for example <c>Cowork, 2 running</c>.</summary>
    /// <param name="surfaceName">The surface word, <c>Code</c> or <c>Cowork</c>.</param>
    /// <param name="running">The running count. At least 1.</param>
    /// <returns>The chip text.</returns>
    public static string RunningChip(string surfaceName, int running) => surfaceName + ", " + running.ToString(System.Globalization.CultureInfo.InvariantCulture) + " running";

    /// <summary>Builds the hidden-surface chip when an approval is waiting, for example <c>Code, needs approval</c>.</summary>
    /// <param name="surfaceName">The surface word, <c>Code</c> or <c>Cowork</c>.</param>
    /// <returns>The chip text.</returns>
    public static string NeedsApprovalChip(string surfaceName) => surfaceName + ", needs approval";

    /// <summary>Builds the hidden-surface chip for a failed count when nothing is running or waiting.</summary>
    /// <param name="surfaceName">The surface word, <c>Code</c> or <c>Cowork</c>.</param>
    /// <param name="failed">The failed count. At least 1.</param>
    /// <returns>The chip text.</returns>
    public static string FailedChip(string surfaceName, int failed) => surfaceName + ", " + failed.ToString(System.Globalization.CultureInfo.InvariantCulture) + " failed";
}

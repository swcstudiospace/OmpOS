using System.Globalization;

namespace Autonogrammer.Presentation.Settings.Workspaces;

/// <summary>
/// Every text of the Workspaces settings page, in one place so the view carries no literal. No sentence holds a credential; folder
/// paths appear only where the user typed or chose them.
/// </summary>
public static class WorkspacesCopy
{
    /// <summary>The navigation label of the Workspaces page.</summary>
    public const string Title = "Workspaces";

    /// <summary>The heading of the Workspaces page.</summary>
    public const string Heading = "Workspaces";

    /// <summary>The sentence under the heading.</summary>
    public const string Description =
        "A workspace set is one or more folders a session can work in. The first folder is the primary: the agent runs there, and the other folders are added to it as extra directories.";

    /// <summary>The name a set has until the user renames it, and the name Reset returns to.</summary>
    public const string DefaultName = "Workspace";

    /// <summary>The heading of the editor of the current set.</summary>
    public const string EditorHeading = "Current set";

    /// <summary>The explanation of how a set is kept.</summary>
    public const string EditorHelp = "Changes stay in the editor until you save them. Saving keeps the set in the list of recent sets under its name; a set with the same name is replaced.";

    /// <summary>The accessible name of the embedded folder editor.</summary>
    public const string EditorName = "Workspace set folders";

    /// <summary>The heading of the list of recent sets.</summary>
    public const string RecentsHeading = "Recent sets";

    /// <summary>The sentence under the recents heading. The slash commands read the same list.</summary>
    public const string RecentsHelp = "These are the sets you saved here or with /save-workspace. /load-workspace opens the same sets.";

    /// <summary>The text shown when no set was saved yet.</summary>
    public const string NoRecents = "No workspace set has been saved yet. Add folders above and save the set to see it here.";

    /// <summary>The accessible name of the list of recent sets.</summary>
    public const string RecentsListName = "Recent workspace sets";

    /// <summary>The label of the button that opens a recent set in the editor.</summary>
    public const string LoadLabel = "Load into editor";

    /// <summary>The label of the button that removes a set from the list of recent sets.</summary>
    public const string ForgetLabel = "Forget";

    /// <summary>The accessible name of the polite live region that announces what a button did.</summary>
    public const string AnnouncementName = "Workspace set update";

    /// <summary>The heading of the inline save error.</summary>
    public const string SaveFailedHeading = "The workspace set was not saved.";

    /// <summary>The sentence shown when the set has no folder.</summary>
    public const string NoFolders = "Add at least one folder before saving the set.";

    /// <summary>The reason shown when a save failed and the source gave none.</summary>
    public const string SaveFailedFallback = "The workspace sets file could not be written.";

    /// <summary>The announcement when a set could not be forgotten and the source gave no reason.</summary>
    public const string ForgetFailedFallback = "The set could not be forgotten.";

    /// <summary>Says how many folders a set has: "1 folder" or "N folders".</summary>
    /// <param name="count">The number of folders.</param>
    /// <returns>The phrase.</returns>
    public static string FolderCount(int count) =>
        count == 1 ? "1 folder" : string.Create(CultureInfo.InvariantCulture, $"{count} folders");

    /// <summary>Says why a set was not saved because the editor holds a blocking finding.</summary>
    /// <param name="finding">The first blocking finding, as the checker worded it.</param>
    /// <returns>The sentence.</returns>
    public static string CannotSave(string finding) =>
        string.Create(CultureInfo.InvariantCulture, $"The set cannot be saved yet. {finding}");

    /// <summary>Gets the sentence that says when a set was last used.</summary>
    /// <param name="when">The time, already formatted by the host.</param>
    /// <returns>The sentence.</returns>
    public static string LastUsed(string when) => string.Create(CultureInfo.InvariantCulture, $"Last used {when}.");

    /// <summary>Gets the one-sentence summary of a recent set, used as its accessible name.</summary>
    /// <param name="name">The name of the set.</param>
    /// <param name="folderCount">The number of folders.</param>
    /// <param name="primaryPath">The path of the primary folder.</param>
    /// <param name="lastUsed">The last-used sentence.</param>
    /// <returns>The summary.</returns>
    public static string RecentSummary(string name, int folderCount, string primaryPath, string lastUsed) =>
        string.Create(CultureInfo.InvariantCulture, $"{name}, {FolderCount(folderCount)}, primary {primaryPath}. {lastUsed}");

    /// <summary>Gets the accessible name of the Load into editor button of one recent set.</summary>
    /// <param name="name">The name of the set.</param>
    /// <returns>The name.</returns>
    public static string LoadName(string name) => string.Create(CultureInfo.InvariantCulture, $"Load {name} into the editor");

    /// <summary>Gets the accessible name of the Forget button of one recent set.</summary>
    /// <param name="name">The name of the set.</param>
    /// <returns>The name.</returns>
    public static string ForgetName(string name) => string.Create(CultureInfo.InvariantCulture, $"Forget {name}");

    /// <summary>Gets the announcement after a set was saved.</summary>
    /// <param name="name">The name of the set.</param>
    /// <returns>The announcement.</returns>
    public static string Saved(string name) => string.Create(CultureInfo.InvariantCulture, $"Saved {name}.");

    /// <summary>Gets the announcement after a recent set was opened in the editor.</summary>
    /// <param name="name">The name of the set.</param>
    /// <returns>The announcement.</returns>
    public static string Loaded(string name) =>
        string.Create(CultureInfo.InvariantCulture, $"Loaded {name} into the editor. Save to keep it as the current set.");

    /// <summary>Gets the announcement when a recent set cannot be opened because its folders do not form a set.</summary>
    /// <param name="name">The name of the set.</param>
    /// <returns>The announcement.</returns>
    public static string RecentUnusable(string name) =>
        string.Create(CultureInfo.InvariantCulture, $"{name} cannot be loaded: its saved folders are not a valid set. Forget it and save it again.");

    /// <summary>Gets the announcement after a set was forgotten.</summary>
    /// <param name="name">The name of the set.</param>
    /// <returns>The announcement.</returns>
    public static string Forgot(string name) => string.Create(CultureInfo.InvariantCulture, $"Forgot {name}.");

    /// <summary>Gets the announcement when a set to forget was no longer in the list.</summary>
    /// <param name="name">The name of the set.</param>
    /// <returns>The announcement.</returns>
    public static string AlreadyForgotten(string name) =>
        string.Create(CultureInfo.InvariantCulture, $"{name} was already gone from the list.");
}

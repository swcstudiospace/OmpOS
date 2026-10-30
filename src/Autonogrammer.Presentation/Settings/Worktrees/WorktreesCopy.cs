using System.Globalization;

namespace Autonogrammer.Presentation.Settings.Worktrees;

/// <summary>Every text of the Worktrees page, in one place so the view carries no literal. The page is read-only.</summary>
public static class WorktreesCopy
{
    /// <summary>The navigation label of the Worktrees page.</summary>
    public const string Title = "Worktrees";

    /// <summary>The heading of the Worktrees page.</summary>
    public const string Heading = "Worktrees";

    /// <summary>The sentence under the heading.</summary>
    public const string Description = "How Autonogrammer gives an agent run its own git worktree. This page shows the facts; none of them can be changed here.";

    /// <summary>The label of the workspace root.</summary>
    public const string WorkspaceLabel = "Workspace";

    /// <summary>The label of the storage location.</summary>
    public const string StorageLabel = "Where worktrees are created";

    /// <summary>The storage statement.</summary>
    public const string StorageText =
        "Each run gets its own folder, named after the run, in the worktrees folder of the Autonogrammer state directory, on the branch autonogrammer/run/ followed by the run name.";

    /// <summary>The label of the worktree cap.</summary>
    public const string CapLabel = "Most worktrees";

    /// <summary>The label of the clean-up statement.</summary>
    public const string CleanupLabel = "Clean-up";

    /// <summary>The clean-up statement. It says what Autonogrammer never does and what Cursor setting is not implemented.</summary>
    public const string CleanupText =
        "Autonogrammer never removes a worktree by itself. Use /delete-worktree to remove one. Cursor's automatic clean-up interval (cursor.worktreeCleanupIntervalHours) is not implemented.";

    /// <summary>The heading of the setup section.</summary>
    public const string SetupHeading = "Setup commands";

    /// <summary>The sentence under the setup heading.</summary>
    public const string SetupDescription =
        "A new worktree can run the commands of .cursor/worktrees.json. They never run until you approve them.";

    /// <summary>The label of the setup file path.</summary>
    public const string SetupFileLabel = "File";

    /// <summary>The label of the script path.</summary>
    public const string ScriptLabel = "Script";

    /// <summary>The label of the command list.</summary>
    public const string CommandsLabel = "Commands";

    /// <summary>The status when the workspace has no setup file.</summary>
    public const string NoSetupFile = "There is no .cursor/worktrees.json in this workspace, so nothing runs when a worktree is created.";

    /// <summary>The status when the file has no setup for this system.</summary>
    public const string NoSetupForThisSystem = "The file has no setup for this system, so nothing runs when a worktree is created.";

    /// <summary>The status when the setup was not approved.</summary>
    public const string NotGranted = "Commands will not run until you approve them when the first worktree is created.";

    /// <summary>The status when the setup was approved.</summary>
    public const string Granted = "You approved this exact version of the file. Editing it asks again.";

    /// <summary>The status when the setup cannot be read.</summary>
    public const string Invalid = "The setup could not be read, so nothing runs.";

    /// <summary>The label of the Refresh button.</summary>
    public const string RefreshLabel = "Refresh";

    /// <summary>The sentence under the Refresh button.</summary>
    public const string RefreshHelp = "Reads the setup file and your approval again.";

    /// <summary>Builds the statement of the worktree cap. It says the cap is fixed in this build.</summary>
    /// <param name="max">The cap.</param>
    /// <returns>The sentence.</returns>
    public static string CapText(int max) => string.Create(
        CultureInfo.InvariantCulture,
        $"{max} (Cursor's cursor.worktreeMaxCount default). The number is fixed in this build.");

    /// <summary>Gets the status sentence of a setup state.</summary>
    /// <param name="state">The state.</param>
    /// <returns>The sentence.</returns>
    public static string StateText(WorktreeSetupState state) => state switch
    {
        WorktreeSetupState.NoSetupFile => NoSetupFile,
        WorktreeSetupState.NoSetupForThisSystem => NoSetupForThisSystem,
        WorktreeSetupState.NotGranted => NotGranted,
        WorktreeSetupState.Granted => Granted,
        _ => Invalid,
    };
}

namespace Autonogrammer.Native.Views.Workspace;

/// <summary>
/// Every sentence the workspace set picker view and its window show. The picker model carries no copy of its own, so the words live here, in one place,
/// and the views and the tests read the same constants.
/// </summary>
internal static class WorkspaceSetPickerCopy
{
    /// <summary>The title of the picker window.</summary>
    public const string WindowTitle = "Workspace folders";

    /// <summary>The label and accessible name of the set name box.</summary>
    public const string NameLabel = "Workspace name";

    /// <summary>The heading above the folder list.</summary>
    public const string FoldersHeading = "Folders";

    /// <summary>The accessible name of the folder list.</summary>
    public const string FolderListName = "Folders in the workspace";

    /// <summary>The sentence shown while the set has no folder.</summary>
    public const string EmptyText = "No folders yet. Add a folder, or import a .code-workspace file.";

    /// <summary>The marker on the primary folder, the working directory of the run.</summary>
    public const string PrimaryLabel = "Primary";

    /// <summary>The label of the button that makes a folder the primary.</summary>
    public const string MakePrimaryLabel = "Make primary";

    /// <summary>The label of the button that removes a folder.</summary>
    public const string RemoveLabel = "Remove";

    /// <summary>The label of the Add folder button.</summary>
    public const string AddFolderLabel = "Add folder";

    /// <summary>The label of the Import button.</summary>
    public const string ImportLabel = "Import .code-workspace";

    /// <summary>The label of the Export button.</summary>
    public const string ExportLabel = "Export .code-workspace";

    /// <summary>The word in front of a finding that blocks the set, so the severity is not told by colour alone.</summary>
    public const string ErrorPrefix = "Error:";

    /// <summary>The word in front of a finding that does not block the set.</summary>
    public const string WarningPrefix = "Warning:";

    /// <summary>The label of the primary button that starts a session over the set.</summary>
    public const string StartLabel = "Start";

    /// <summary>The label of the button that closes the picker without starting.</summary>
    public const string DoneLabel = "Done";

    /// <summary>The heading above the findings about the set.</summary>
    public const string FindingsHeading = "Findings";

    /// <summary>The accessible name of the findings list, a polite live region.</summary>
    public const string FindingsListName = "Findings about the workspace";

    /// <summary>The accessible name of the list of entries an import could not use, a polite live region.</summary>
    public const string ImportWarningsName = "Entries the import skipped";

    /// <summary>The accessible name of the line that says why an import or export did not happen, an assertive live region.</summary>
    public const string LastErrorName = "Import or export error";

    /// <summary>The line next to Start while the set cannot start.</summary>
    public const string CannotStartHint = "Add a folder and fix the findings to start.";

    /// <summary>The title of the folder dialog.</summary>
    public const string PickFolderTitle = "Add a folder to the workspace";

    /// <summary>The title of the import dialog.</summary>
    public const string PickImportTitle = "Import a .code-workspace file";

    /// <summary>The title of the export dialog.</summary>
    public const string PickExportTitle = "Export the workspace as a .code-workspace file";

    /// <summary>The name of the .code-workspace file type in the file dialogs.</summary>
    public const string CodeWorkspaceTypeName = "VS Code workspace";

    /// <summary>The file pattern of a .code-workspace file.</summary>
    public const string CodeWorkspacePattern = "*.code-workspace";

    /// <summary>The extension, without the dot, a saved .code-workspace file gets by default.</summary>
    public const string CodeWorkspaceExtension = "code-workspace";

    /// <summary>Builds the accessible name of the button that makes a folder the primary.</summary>
    /// <param name="displayName">The name of the folder.</param>
    /// <returns>The name.</returns>
    public static string MakePrimaryName(string displayName) => "Make " + displayName + " the primary folder";

    /// <summary>Builds the accessible name of the button that removes a folder.</summary>
    /// <param name="displayName">The name of the folder.</param>
    /// <returns>The name.</returns>
    public static string RemoveName(string displayName) => "Remove folder " + displayName;
}

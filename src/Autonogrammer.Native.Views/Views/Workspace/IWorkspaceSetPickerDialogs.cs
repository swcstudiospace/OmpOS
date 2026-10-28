namespace Autonogrammer.Native.Views.Workspace;

/// <summary>
/// The file and folder dialogs of the workspace set picker. The desktop head uses the storage provider of the window; tests script the answers.
/// Every method answers a local path, or null when the user cancelled or the platform gave no local path.
/// </summary>
internal interface IWorkspaceSetPickerDialogs
{
    /// <summary>Asks the user for a folder to add.</summary>
    /// <param name="cancellationToken">Cancels the dialog.</param>
    /// <returns>The local path of the folder, or null.</returns>
    Task<string?> PickFolderAsync(CancellationToken cancellationToken);

    /// <summary>Asks the user for a <c>.code-workspace</c> file to import.</summary>
    /// <param name="cancellationToken">Cancels the dialog.</param>
    /// <returns>The local path of the file, or null.</returns>
    Task<string?> PickImportFileAsync(CancellationToken cancellationToken);

    /// <summary>Asks the user where to write a <c>.code-workspace</c> file.</summary>
    /// <param name="suggestedName">The file name the dialog offers, with its extension.</param>
    /// <param name="cancellationToken">Cancels the dialog.</param>
    /// <returns>The local path to write, or null.</returns>
    Task<string?> PickExportFileAsync(string suggestedName, CancellationToken cancellationToken);
}

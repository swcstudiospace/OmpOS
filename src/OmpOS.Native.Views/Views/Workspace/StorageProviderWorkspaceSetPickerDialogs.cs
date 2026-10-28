using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace OmpOS.Native.Views.Workspace;

/// <summary>
/// The dialogs of the workspace set picker over the storage provider of a top level, so the platform's own folder and file dialogs open.
/// Only local paths are answered: an item the platform cannot give as a local path counts as no answer.
/// </summary>
internal sealed class StorageProviderWorkspaceSetPickerDialogs : IWorkspaceSetPickerDialogs
{
    private readonly Func<TopLevel?> _topLevel;

    /// <summary>Creates the dialogs over a top level that is looked up each time a dialog opens, so the answer follows the window the view is in.</summary>
    /// <param name="topLevel">Finds the top level that owns the dialogs, or null when the view is not on screen.</param>
    /// <exception cref="ArgumentNullException">The lookup is null.</exception>
    public StorageProviderWorkspaceSetPickerDialogs(Func<TopLevel?> topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);

        _topLevel = topLevel;
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(CancellationToken cancellationToken)
    {
        if (_topLevel()?.StorageProvider is not { CanPickFolder: true } provider)
        {
            return null;
        }

        var options = new FolderPickerOpenOptions
        {
            Title = WorkspaceSetPickerCopy.PickFolderTitle,
            AllowMultiple = false,
        };
        var folders = await provider.OpenFolderPickerAsync(options).WaitAsync(cancellationToken).ConfigureAwait(true);
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickImportFileAsync(CancellationToken cancellationToken)
    {
        if (_topLevel()?.StorageProvider is not { CanOpen: true } provider)
        {
            return null;
        }

        var options = new FilePickerOpenOptions
        {
            Title = WorkspaceSetPickerCopy.PickImportTitle,
            AllowMultiple = false,
            FileTypeFilter = [CodeWorkspaceType()],
        };
        var files = await provider.OpenFilePickerAsync(options).WaitAsync(cancellationToken).ConfigureAwait(true);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    /// <inheritdoc />
    public async Task<string?> PickExportFileAsync(string suggestedName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedName);

        if (_topLevel()?.StorageProvider is not { CanSave: true } provider)
        {
            return null;
        }

        var options = new FilePickerSaveOptions
        {
            Title = WorkspaceSetPickerCopy.PickExportTitle,
            SuggestedFileName = suggestedName,
            DefaultExtension = WorkspaceSetPickerCopy.CodeWorkspaceExtension,
            FileTypeChoices = [CodeWorkspaceType()],
            ShowOverwritePrompt = true,
        };
        var file = await provider.SaveFilePickerAsync(options).WaitAsync(cancellationToken).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }

    private static FilePickerFileType CodeWorkspaceType() =>
        new(WorkspaceSetPickerCopy.CodeWorkspaceTypeName) { Patterns = [WorkspaceSetPickerCopy.CodeWorkspacePattern] };
}

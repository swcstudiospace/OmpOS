using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Presentation.Settings.Workspaces;

/// <summary>
/// What the Workspaces page needs from the host: the validation and <c>.code-workspace</c> access of the folder editor, and the
/// list of remembered sets that the <c>/save-workspace</c> and <c>/load-workspace</c> commands share. The desktop head implements it
/// over the workspace set store and validator.
/// </summary>
public interface IWorkspacesSettingsSource
{
    /// <summary>Gets the validation and file access the folder editor of the page uses.</summary>
    IWorkspaceSetPickerBackend PickerBackend { get; }

    /// <summary>Lists the remembered sets, newest first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The sets.</returns>
    /// <exception cref="SettingsSourceException">The list cannot be read.</exception>
    Task<IReadOnlyList<WorkspaceRecentRow>> ListRecentsAsync(CancellationToken cancellationToken);

    /// <summary>Remembers a set under a name. A set of the same name is replaced.</summary>
    /// <param name="name">The name of the set.</param>
    /// <param name="roots">The folders, primary first.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the set is remembered.</returns>
    /// <exception cref="SettingsSourceException">The set cannot be written.</exception>
    Task SaveAsync(string name, IReadOnlyList<WorkspacePickerRoot> roots, CancellationToken cancellationToken);

    /// <summary>Forgets a remembered set.</summary>
    /// <param name="setId">The identifier of the set.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>True when a set was removed; false when no set had the identifier.</returns>
    /// <exception cref="SettingsSourceException">The list cannot be written.</exception>
    Task<bool> ForgetAsync(string setId, CancellationToken cancellationToken);
}

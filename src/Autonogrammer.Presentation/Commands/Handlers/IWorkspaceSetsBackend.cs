namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>
/// The saved workspaces: named sets of folders. The desktop head implements it over <c>WorkspaceSetStore</c> and <c>WorkspaceSet</c> of <c>Autonogrammer.Harness.Workspace</c>: saving renames the open set and records it,
/// loading finds the set by name and opens it in the workspace picker.
/// </summary>
public interface IWorkspaceSetsBackend
{
    /// <summary>Lists the names of the saved workspaces, newest first.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The names.</returns>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Saves the open folders under a name, replacing a saved workspace of the same name.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>A task that completes when the workspace is saved.</returns>
    Task SaveAsync(string name, CancellationToken cancellationToken);

    /// <summary>Opens a saved workspace.</summary>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="false"/> when no workspace has that name.</returns>
    Task<bool> LoadAsync(string name, CancellationToken cancellationToken);
}

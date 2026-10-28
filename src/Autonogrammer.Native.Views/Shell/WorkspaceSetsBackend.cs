using Autonogrammer.Presentation.Commands.Handlers;
using Autonogrammer.Presentation.Editor;
using Autonogrammer.Workspace.Sets;

namespace Autonogrammer.Native.Views.Shell;

/// <summary>
/// The saved workspace sets behind <c>/save-workspace</c> and <c>/load-workspace</c>. Saving records the folders of the current session under a name; loading starts a new session on a saved set, through the window's
/// <c>startOnSet</c> callback.
/// </summary>
/// <param name="store">The store of saved sets.</param>
/// <param name="currentFolders">Gives the folders of the session the command runs in (the primary first).</param>
/// <param name="startOnSet">Starts a new session on a set's folders (the first is the primary) and says whether it started.</param>
internal sealed class WorkspaceSetsBackend(WorkspaceSetStore store, Func<IReadOnlyList<string>> currentFolders, Func<IReadOnlyList<WorkspacePickerRoot>, CancellationToken, Task<bool>> startOnSet) : IWorkspaceSetsBackend
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken)
    {
        var sets = await store.ListAsync(cancellationToken).ConfigureAwait(true);
        return [.. sets.Select(static recent => recent.Set.Name)];
    }

    /// <inheritdoc/>
    public async Task SaveAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var folders = currentFolders();
        if (folders.Count == 0)
        {
            throw new InvalidOperationException("This session has no folder to save.");
        }

        await store.RecordAsync(WorkspaceSet.FromPaths(name, folders), cancellationToken).ConfigureAwait(true);
    }

    /// <inheritdoc/>
    public async Task<bool> LoadAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var sets = await store.ListAsync(cancellationToken).ConfigureAwait(true);
        var match = sets.FirstOrDefault(recent => string.Equals(recent.Set.Name, name, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            return false;
        }

        var roots = match.Set.Roots.Select(static root => new WorkspacePickerRoot(root.Id, root.Path, root.DisplayName, root.EnvironmentId, root.IsPrimary)).ToArray();
        return await startOnSet(roots, cancellationToken).ConfigureAwait(true);
    }
}

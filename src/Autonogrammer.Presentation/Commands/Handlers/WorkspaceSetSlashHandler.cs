namespace Autonogrammer.Presentation.Commands.Handlers;

/// <summary>
/// Runs <c>/save-workspace name</c> (<see cref="HandlerActionIds.WorkspaceSave"/>) and <c>/load-workspace [name]</c> (<see cref="HandlerActionIds.WorkspaceLoad"/>) over <see cref="IWorkspaceSetsBackend"/>. Loading without a name
/// lists the saved workspaces. A name is up to 80 characters without control characters.
/// </summary>
public sealed class WorkspaceSetSlashHandler : ISlashActionHandler
{
    private const int MaxNameChars = 80;

    private readonly IWorkspaceSetsBackend _backend;

    /// <summary>Creates the handler of one action.</summary>
    /// <param name="actionId"><see cref="HandlerActionIds.WorkspaceSave"/> or <see cref="HandlerActionIds.WorkspaceLoad"/>.</param>
    /// <param name="backend">Keeps the saved workspaces.</param>
    /// <exception cref="ArgumentException"><paramref name="actionId"/> is neither.</exception>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public WorkspaceSetSlashHandler(string actionId, IWorkspaceSetsBackend backend)
    {
        ArgumentNullException.ThrowIfNull(actionId);
        ArgumentNullException.ThrowIfNull(backend);
        if (actionId is not (HandlerActionIds.WorkspaceSave or HandlerActionIds.WorkspaceLoad))
        {
            throw new ArgumentException("Not a workspace action: " + actionId, nameof(actionId));
        }

        ActionId = actionId;
        _backend = backend;
    }

    /// <inheritdoc/>
    public string ActionId { get; }

    /// <inheritdoc/>
    public bool CanRun => true;

    /// <inheritdoc/>
    public async Task<SlashActionResult> RunAsync(SlashActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = request.TypedText.Trim();
        if (name.Length > MaxNameChars || name.Any(char.IsControl))
        {
            return SlashActionResult.Failed(HandlerCopy.WorkspaceBadName);
        }

        if (string.Equals(ActionId, HandlerActionIds.WorkspaceSave, StringComparison.Ordinal))
        {
            if (name.Length == 0)
            {
                return SlashActionResult.Failed(HandlerCopy.WorkspaceNeedsName);
            }

            await _backend.SaveAsync(name, cancellationToken).ConfigureAwait(true);
            return SlashActionResult.Completed(HandlerCopy.WorkspaceSaved(name));
        }

        var known = await _backend.ListAsync(cancellationToken).ConfigureAwait(true);
        if (name.Length == 0)
        {
            return SlashActionResult.Completed(HandlerCopy.WorkspaceList(known));
        }

        return await _backend.LoadAsync(name, cancellationToken).ConfigureAwait(true)
            ? SlashActionResult.Completed(HandlerCopy.WorkspaceLoaded(name))
            : SlashActionResult.Failed(HandlerCopy.WorkspaceUnknown(name, known));
    }
}

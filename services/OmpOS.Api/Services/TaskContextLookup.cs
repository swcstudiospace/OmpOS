using OmpOS.Contracts.Agents;
using OmpOS.Worktrees;

namespace OmpOS.Api.Services;

/// <summary>
/// Reads the task context of a session from the host's <see cref="ITaskContextSource"/>. The API ships no source: a host registers one, and
/// until it does the lookup answers with a reason instead of an invented context.
/// </summary>
internal static class TaskContextLookup
{
    /// <summary>Reads the context.</summary>
    /// <param name="services">The request services, which may hold an <see cref="ITaskContextSource"/>.</param>
    /// <param name="sessionId">The identifier of a session the caller owns.</param>
    /// <param name="graphId">The Graph identifier a stage source reported, or null.</param>
    /// <param name="workspace">The workspace locator.</param>
    /// <param name="worktrees">The worktree options, which name the repository and the state root when no workspace root is set.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The context or the reason there is none.</returns>
    public static async Task<TaskContextLookupResult> ReadAsync(
        IServiceProvider services,
        string sessionId,
        string? graphId,
        WorkspaceLocator workspace,
        WorktreesOptions worktrees,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(worktrees);
        if (services.GetService(typeof(ITaskContextSource)) is not ITaskContextSource source)
        {
            return new TaskContextLookupResult(null, "No task-context source is connected to this API.");
        }

        var root = workspace.Root ?? (Path.IsPathFullyQualified(worktrees.Repository) ? worktrees.Repository : null);
        if (root is null)
        {
            return new TaskContextLookupResult(null, "The API has no workspace folder to read the task context from.");
        }

        var stateRoot = Path.IsPathFullyQualified(worktrees.Root) ? worktrees.Root : root;
        var scope = new StageObservationScope
        {
            SessionId = sessionId,
            WorkspaceRoot = root,
            SessionStateDirectory = Path.Combine(stateRoot, sessionId),
            GraphId = graphId,
        };
        var context = await source.GetAsync(scope, cancellationToken).ConfigureAwait(false);
        return new TaskContextLookupResult(context, null);
    }
}

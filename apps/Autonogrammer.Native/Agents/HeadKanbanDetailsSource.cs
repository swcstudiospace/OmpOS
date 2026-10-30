using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// Supplies the Substrate fields of a Kanban card from the session's own task context. A field is reported only when the Substrate gave it:
/// the task title only when the Substrate named a task node (the workflow phase title that stands in for it is not a Substrate fact), the
/// lessons count only when the lessons were read, and the lease holder never, because the task context carries no claim holder. A session
/// whose Substrate is not active, or whose context could not be read, is reported as not available with every field empty.
/// </summary>
internal sealed class HeadKanbanDetailsSource : IKanbanCardDetailsSource
{
    private const string ContextUnavailablePrefix = "Substrate context is unavailable";
    private const string LessonsWord = "lessons";

    private static readonly KanbanCardDetails NotAvailable = new(null, null, null, null, Available: false);

    private readonly SessionEngineSet _host;

    /// <summary>Creates the source over the engine set that owns the sessions.</summary>
    /// <param name="host">The engine set.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
    public HeadKanbanDetailsSource(SessionEngineSet host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <inheritdoc />
    public async Task<KanbanCardDetails?> LoadAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        var registry = _host.Registry;
        if (registry is null || !registry.TryGet(sessionId, out var record))
        {
            return null;
        }

        TaskContext context;
        try
        {
            var slot = _host.CreateSlot(sessionId, record.WorkspaceRoot, record.GraphId);
            var plan = await slot.PlanAsync(cancellationToken).ConfigureAwait(false);
            if (!plan.SubstrateActive)
            {
                return NotAvailable;
            }

            context = await slot.Context.GetAsync(slot.Scope.Current, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return NotAvailable;
        }

        return Map(context);
    }

    /// <summary>Maps a task context to the fields of a card.</summary>
    /// <param name="context">What the session's context source read.</param>
    /// <returns>The details. The lease holder is always null: the task context has no claim holder to report.</returns>
    internal static KanbanCardDetails Map(TaskContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Warnings.Any(static warning => warning.StartsWith(ContextUnavailablePrefix, StringComparison.Ordinal)))
        {
            return NotAvailable;
        }

        var lessonsRead = !context.Warnings.Any(static warning => warning.Contains(LessonsWord, StringComparison.OrdinalIgnoreCase));
        return new KanbanCardDetails(
            context.TaskId is null ? null : context.Title,
            string.IsNullOrWhiteSpace(context.GraphId) ? null : context.GraphId,
            lessonsRead ? context.Lessons.Count : null,
            LeaseHolder: null,
            Available: true);
    }
}

using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Web.Services;

/// <summary>The <see cref="IKanbanCardDetailsSource"/> of the web head: <c>GET /sessions/{id}/card-details</c>.</summary>
internal sealed class WebKanbanDetailsSource : IKanbanCardDetailsSource
{
    private readonly IOmpApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="WebKanbanDetailsSource"/> class.</summary>
    /// <param name="api">The API client.</param>
    internal WebKanbanDetailsSource(IOmpApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <inheritdoc />
    public async Task<KanbanCardDetails?> LoadAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var details = await _api.GetCardDetailsAsync(sessionId, cancellationToken).ConfigureAwait(true);
            return new KanbanCardDetails(details.TaskTitle, details.GraphId, details.LessonsCount, details.LeaseHolder, details.Available);
        }
        catch (ApiProblemException problem) when (problem.Kind == ApiProblemKind.NotFound)
        {
            return null;
        }
    }
}

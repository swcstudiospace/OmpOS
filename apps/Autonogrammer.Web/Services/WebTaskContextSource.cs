using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The <see cref="ITaskContextSource"/> of the web head: <c>GET /sessions/{id}/context</c>. When the API reports the context unavailable, the
/// returned context is empty and carries the API's reason as its one warning, so the panel shows "unavailable" with that reason.
/// </summary>
internal sealed class WebTaskContextSource : ITaskContextSource
{
    private readonly IOmpApiClient _api;

    /// <summary>Initializes a new instance of the <see cref="WebTaskContextSource"/> class.</summary>
    /// <param name="api">The API client.</param>
    internal WebTaskContextSource(IOmpApiClient api)
    {
        ArgumentNullException.ThrowIfNull(api);
        _api = api;
    }

    /// <inheritdoc />
    public async Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var response = await _api.GetContextAsync(scope.SessionId, cancellationToken).ConfigureAwait(true);
        if (response is { Available: true, Context: { } context })
        {
            return context;
        }

        IReadOnlyList<string> warnings = string.IsNullOrWhiteSpace(response.Reason) ? [] : [response.Reason];
        return new TaskContext { Title = string.Empty, SharedMemory = [], Lessons = [], Warnings = warnings };
    }
}

using System.Text.Json;
using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Catalog;
using OmpOS.Contracts.Controls;
using OmpOS.Sessions;

namespace OmpOS.Api.Services;

/// <summary>
/// Holds the latest stage event of each session, source and stage, as plugin stage sources report them. A published stage is appended to the
/// session's log as a <c>stage</c> event and announced on the user-level stream. The hub is the one place a stage source of the host
/// connects to; the API itself publishes none, so a host without a connected source answers with unavailable stages.
/// </summary>
internal sealed class StageHub
{
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<string, Dictionary<(StageSource Source, string StageId), StageEvent>> _stages = new(StringComparer.Ordinal);
    private readonly ISessionRepository _repository;
    private readonly SessionLogAppender _appender;
    private readonly CatalogEventHub _hub;

    /// <summary>Initializes a new instance of the <see cref="StageHub"/> class.</summary>
    /// <param name="repository">The session store.</param>
    /// <param name="appender">Appends the stage to the session's log.</param>
    /// <param name="hub">The user-level event history.</param>
    public StageHub(ISessionRepository repository, SessionLogAppender appender, CatalogEventHub hub)
    {
        _repository = repository;
        _appender = appender;
        _hub = hub;
    }

    /// <summary>Publishes a stage observation for an existing session.</summary>
    /// <param name="stage">The stage event. A later event for the same session, source and stage replaces the earlier one.</param>
    /// <param name="cancellationToken">Cancels the publish.</param>
    /// <returns>True when published; false when the session does not exist.</returns>
    public async Task<bool> PublishAsync(StageEvent stage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stage);
        var session = await _repository.GetAsync(stage.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return false;
        }

        lock (_gate)
        {
            if (!_stages.TryGetValue(stage.SessionId, out var bySession))
            {
                bySession = new Dictionary<(StageSource Source, string StageId), StageEvent>();
                _stages[stage.SessionId] = bySession;
            }

            bySession[(stage.Source, stage.StageId)] = stage;
        }

        _ = await _appender.AppendAsync(stage.SessionId, StreamEventPayloads.CreateStage(stage), cancellationToken).ConfigureAwait(false);
        _ = _hub.Publish(session.OwnerId, CatalogEventKinds.Stage, JsonSerializer.Serialize(stage, CatalogJsonContext.Default.StageEvent));
        return true;
    }

    /// <summary>Gets the latest stage events of a session, in pipeline order.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The published stage events.</returns>
    public IReadOnlyList<StageEvent> Snapshot(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        lock (_gate)
        {
            return _stages.TryGetValue(sessionId, out var bySession)
                ? [.. bySession.Values.OrderBy(static stage => stage.Source).ThenBy(static stage => stage.StageId, StringComparer.Ordinal)]
                : [];
        }
    }

    /// <summary>Gets the Graph identifier a stage source reported for a session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The identifier of the latest stage that carries one, or null.</returns>
    public string? GraphIdOf(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        lock (_gate)
        {
            return _stages.TryGetValue(sessionId, out var bySession)
                ? bySession.Values.Where(static stage => stage.GraphId is not null).OrderByDescending(static stage => stage.Timestamp).Select(static stage => stage.GraphId).FirstOrDefault()
                : null;
        }
    }
}

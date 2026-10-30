using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.Services;

/// <summary>
/// Finds what changed in the catalog and publishes it on the user-level stream. The session store has no change feed for a whole catalog, so
/// this reads each session, compares it with what it saw before and publishes the difference: a new session, a closed or reopened one, a
/// new status, a new engine stage. It scans on a timer while a stream is connected, and on demand after an operation that changes the
/// catalog.
/// </summary>
internal sealed class CatalogChangeDetector : BackgroundService
{
    /// <summary>How often the detector scans while a stream is connected.</summary>
    public static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(250);

    private static readonly Action<ILogger, string, Exception?> ScanFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "CatalogScanFailed"), "The catalog scan failed: {ErrorType}. The next scan retries.");

    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly Dictionary<string, Seen> _seen = new(StringComparer.Ordinal);
    private readonly SessionRegistry _registry;
    private readonly ISessionRepository _repository;
    private readonly SessionLifecycleStore _lifecycle;
    private readonly CatalogEventHub _hub;
    private readonly TimeProvider _time;
    private readonly ILogger<CatalogChangeDetector> _logger;

    /// <summary>Initializes a new instance of the <see cref="CatalogChangeDetector"/> class.</summary>
    /// <param name="registry">The ids of the sessions created through this API.</param>
    /// <param name="repository">The session store.</param>
    /// <param name="lifecycle">The closed flags.</param>
    /// <param name="hub">The user-level event history.</param>
    /// <param name="time">The clock.</param>
    /// <param name="logger">The logger.</param>
    public CatalogChangeDetector(
        SessionRegistry registry,
        ISessionRepository repository,
        SessionLifecycleStore lifecycle,
        CatalogEventHub hub,
        TimeProvider time,
        ILogger<CatalogChangeDetector> logger)
    {
        _registry = registry;
        _repository = repository;
        _lifecycle = lifecycle;
        _hub = hub;
        _time = time;
        _logger = logger;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _scanGate.Dispose();
        base.Dispose();
    }

    /// <summary>Scans the catalog once and publishes every difference from the previous scan.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>A task that completes after the scan.</returns>
    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _time.GetUtcNow();
            foreach (var id in _registry.Ids)
            {
                var session = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
                if (session is null)
                {
                    continue;
                }

                var closed = _lifecycle.IsClosed(id);
                var (row, group) = CatalogProjector.RowOf(session, closed, now);
                var stage = StageProjection.OmpTurn(session);
                var current = new Seen(session.Status, row, group, closed, stage.Status);
                if (!_seen.TryGetValue(id, out var previous))
                {
                    _seen[id] = current;
                    PublishChange(session, CatalogChangeKind.Created, row, group);
                    PublishStatus(session, row, group);
                    PublishStage(session, stage);
                    continue;
                }

                if (previous == current)
                {
                    continue;
                }

                _seen[id] = current;
                if (previous.Closed != closed)
                {
                    PublishChange(session, closed ? CatalogChangeKind.Closed : CatalogChangeKind.Reopened, row, group);
                }
                else if (previous.Row != row || previous.Group != group)
                {
                    PublishChange(session, CatalogChangeKind.Updated, row, group);
                }

                if (previous.Status != session.Status || previous.Row != row || previous.Group != group)
                {
                    PublishStatus(session, row, group);
                }

                if (previous.Stage != stage.Status)
                {
                    PublishStage(session, stage);
                }
            }
        }
        finally
        {
            _ = _scanGate.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(ScanInterval, _time);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            if (_hub.SubscriberCount == 0)
            {
                continue;
            }

            try
            {
                await ScanAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                ScanFailed(_logger, ex.GetType().Name, null);
            }
        }
    }

    private void PublishChange(Session session, CatalogChangeKind kind, CatalogRowStatus row, CatalogRowGroup group) =>
        _ = _hub.Publish(
            session.OwnerId,
            CatalogEventKinds.CatalogChanged,
            JsonSerializer.Serialize(new CatalogChangedEvent { SessionId = session.Id, Change = kind, RowStatus = row, Group = group }, CatalogJsonContext.Default.CatalogChangedEvent));

    private void PublishStatus(Session session, CatalogRowStatus row, CatalogRowGroup group) =>
        _ = _hub.Publish(
            session.OwnerId,
            CatalogEventKinds.SessionStatus,
            JsonSerializer.Serialize(
                new SessionStatusEvent { SessionId = session.Id, Status = session.Status, RowStatus = row, Group = group, LastActivityAt = CatalogProjector.LastActivity(session) },
                CatalogJsonContext.Default.SessionStatusEvent));

    private void PublishStage(Session session, StageEvent stage) =>
        _ = _hub.Publish(session.OwnerId, CatalogEventKinds.Stage, JsonSerializer.Serialize(stage, CatalogJsonContext.Default.StageEvent));

    private sealed record Seen(SessionStatus Status, CatalogRowStatus Row, CatalogRowGroup Group, bool Closed, StageStatus Stage);
}

using Microsoft.Extensions.Options;
using OmpOS.Api.Options;
using OmpOS.Api.Sse;
using OmpOS.Contracts.Sessions;
using OmpOS.Coordination;
using OmpOS.Sessions;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Settles sessions whose run owner has gone. Every <see cref="SweepInterval"/> it lists the sessions still running and the sessions awaiting
/// tool approval, each untouched for longer than <see cref="StaleAfter"/>. A session that no instance holds a run lease for is re-read and,
/// if it is still in the listed status and still stale, settled as failed through <see cref="SessionService.RecordCompletionAsync"/>.
/// </summary>
/// <remarks>
/// The lease is the liveness signal: a run that is still executing holds it and renews it, so only an owner that died or lost its lease leaves
/// a session active without one. The stale threshold keeps a freshly admitted prompt, whose run has not yet begun, out of the sweep.
/// Leases are shared between instances only with the Dragonfly coordination store. With the in-memory store a lease is visible only to the
/// process that holds it, so a live run on another instance would look unowned and be failed wrongly. The reaper is always registered; each
/// <see cref="SweepAsync"/> reads the bound <c>Coordination:Store</c> and returns 0 without reading any session unless it is Dragonfly, so
/// configuration overrides apply.
/// A session awaiting tool approval is settled the same way as a running one: RecordCompletionAsync denies its pending approvals and then
/// applies the failed status, so the approval wait does not outlive its owner.
/// </remarks>
internal sealed class SessionRunReaper : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(60);
    private const string OwnerLostDetail = "run owner lost";

    private static readonly Action<ILogger, string, string, Exception?> SessionSettledAsFailed =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(20, "SessionSettledAsFailed"),
            "Session {SessionId} settled as failed: {Detail}.");

    private static readonly Action<ILogger, Exception?> SweepFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(21, "SessionRunSweepFailed"),
            "Session run sweep failed. The next sweep retries.");

    private readonly ISessionRepository _repository;
    private readonly ISessionRunLeases _leases;
    private readonly SessionService _sessions;
    private readonly SessionChangePublisher _publisher;
    private readonly TimeProvider _time;
    private readonly IOptions<CoordinationOptions> _coordination;
    private readonly ILogger<SessionRunReaper> _logger;

    /// <summary>Initializes a new instance of the <see cref="SessionRunReaper"/> class.</summary>
    /// <param name="repository">The session store.</param>
    /// <param name="leases">The run-lease store.</param>
    /// <param name="sessions">The session service whose completion path settles a session.</param>
    /// <param name="publisher">Announces each settlement to the streams.</param>
    /// <param name="time">The clock that defines staleness.</param>
    /// <param name="coordination">The coordination options; the sweep runs only when the store is Dragonfly.</param>
    /// <param name="logger">The logger.</param>
    public SessionRunReaper(
        ISessionRepository repository,
        ISessionRunLeases leases,
        SessionService sessions,
        SessionChangePublisher publisher,
        TimeProvider time,
        IOptions<CoordinationOptions> coordination,
        ILogger<SessionRunReaper> logger)
    {
        _repository = repository;
        _leases = leases;
        _sessions = sessions;
        _publisher = publisher;
        _time = time;
        _coordination = coordination;
        _logger = logger;
    }

    /// <summary>Settles every stale session in the swept statuses that no instance holds a run lease for.</summary>
    /// <param name="cancellationToken">Cancels the sweep.</param>
    /// <returns>The number of sessions settled by this sweep; 0 when the coordination store is not Dragonfly.</returns>
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        if (!CoordinationOptions.IsDragonflyStore(_coordination.Value.Store))
        {
            return 0;
        }

        var threshold = _time.GetUtcNow() - StaleAfter;
        var settled = await SweepStatusAsync(SessionStatus.Running, threshold, cancellationToken).ConfigureAwait(false);
        settled += await SweepStatusAsync(SessionStatus.AwaitingToolApproval, threshold, cancellationToken).ConfigureAwait(false);
        return settled;
    }

    /// <summary>Sweeps one status: lists its stale sessions and settles each one that has no held lease.</summary>
    /// <param name="status">The status to list.</param>
    /// <param name="threshold">Sessions updated at or after this instant are left alone.</param>
    /// <param name="cancellationToken">Cancels the sweep.</param>
    /// <returns>The number of sessions settled.</returns>
    private async Task<int> SweepStatusAsync(SessionStatus status, DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        var ids = await _repository.ListSessionIdsByStatusAsync(status, threshold, cancellationToken).ConfigureAwait(false);
        var settled = 0;
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _leases.IsHeldAsync(id, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (await TrySettleAsync(id, status, threshold, cancellationToken).ConfigureAwait(false))
            {
                settled++;
            }
        }

        return settled;
    }

    /// <summary>Re-reads one unowned session and settles it when it is still listed, still stale and settleable.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="listed">The status the session was listed under.</param>
    /// <param name="threshold">Sessions updated at or after this instant are left alone.</param>
    /// <param name="cancellationToken">Cancels the settlement.</param>
    /// <returns>True when the session was settled.</returns>
    private async Task<bool> TrySettleAsync(string id, SessionStatus listed, DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        // Re-read after the lease check: the session may have been admitted, settled or touched since the list was taken.
        var session = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (session is null || session.Status != listed || session.UpdatedAt >= threshold)
        {
            return false;
        }

        try
        {
            await _sessions.RecordCompletionAsync(id, SessionStatus.Failed, cancellationToken, failureDetail: OwnerLostDetail).ConfigureAwait(false);
        }
        catch (SessionException)
        {
            // Another writer settled or advanced the session after it was read; there is nothing left to reap.
            return false;
        }

        SessionSettledAsFailed(_logger, id, OwnerLostDetail, null);
        await _publisher.NotifyAsync(id).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // The reaper is a background loop: a failed sweep is logged and the next tick retries.
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                SweepFailed(_logger, null);
            }
#pragma warning restore CA1031
        }
    }
}

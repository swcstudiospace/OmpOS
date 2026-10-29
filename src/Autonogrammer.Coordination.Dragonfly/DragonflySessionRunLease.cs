using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>A lease granted by <see cref="DragonflySessionRunLeases"/>. Every operation is a compare-and-set on the owner id.</summary>
internal sealed class DragonflySessionRunLease : ISessionRunLease
{
    private readonly IDatabase _database;
    private readonly DragonflyResilience _resilience;

    /// <summary>Wraps a lease that was just granted to <paramref name="ownerId"/>.</summary>
    /// <param name="database">The database that holds the lease key.</param>
    /// <param name="resilience">The pipeline of the connection.</param>
    /// <param name="sessionId">The leased session.</param>
    /// <param name="ownerId">The owner that holds the lease.</param>
    public DragonflySessionRunLease(IDatabase database, DragonflyResilience resilience, string sessionId, string ownerId)
    {
        _database = database;
        _resilience = resilience;
        SessionId = sessionId;
        OwnerId = ownerId;
    }

    /// <summary>Gets the session the lease covers.</summary>
    public string SessionId { get; }

    /// <summary>Gets the owner identity that acquired the lease.</summary>
    public string OwnerId { get; }

    /// <summary>Extends the lease only while this owner still holds it.</summary>
    /// <param name="timeToLive">The new lifetime, measured from now.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    /// <returns><see langword="true"/> when the lease was renewed; <see langword="false"/> when it was lost.</returns>
    /// <exception cref="Autonogrammer.Resilience.DataPlaneTimeoutException">Dragonfly could not answer. The owner must treat the lease as lost.</exception>
    public async Task<bool> RenewAsync(TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeToLive, DragonflySessionRunLeases.MinimumTimeToLive);
        _ = DragonflyKeyspace.RequireTimeToLive(timeToLive);
        cancellationToken.ThrowIfCancellationRequested();

        var key = DragonflyKeyspace.LeaseRun(SessionId);
        RedisValue[] values = [OwnerId, (long)timeToLive.TotalMilliseconds];
        var renewed = await _resilience.ClosedAsync(
            true,
            () => _database.ScriptEvaluateAsync(DragonflyScripts.RenewLease, new[] { key }, values),
            cancellationToken).ConfigureAwait(false);
        return (long)renewed == 1;
    }

    /// <summary>
    /// Releases the lease, but only while this owner still holds it. Release is best-effort: when Dragonfly cannot answer the lease
    /// expires on its own within its lifetime, so a disposal never throws an outage into the run that is ending.
    /// </summary>
    /// <returns>A task that completes when the release has been applied or Dragonfly was unreachable.</returns>
    public async ValueTask DisposeAsync()
    {
        var key = DragonflyKeyspace.LeaseRun(SessionId);
        RedisValue[] values = [OwnerId];
        _ = await _resilience.TryOpenAsync(
            () => _database.ScriptEvaluateAsync(DragonflyScripts.DeleteIfValue, new[] { key }, values),
            CancellationToken.None).ConfigureAwait(false);
    }
}

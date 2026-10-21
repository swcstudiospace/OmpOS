using StackExchange.Redis;

namespace OmpOS.Coordination.Dragonfly;

/// <summary>A lease granted by <see cref="DragonflySessionRunLeases"/>. Every operation is a compare-and-set on the owner id.</summary>
internal sealed class DragonflySessionRunLease : ISessionRunLease
{
    private readonly IDatabase _database;

    /// <summary>Wraps a lease that was just granted to <paramref name="ownerId"/>.</summary>
    /// <param name="database">The database that holds the lease key.</param>
    /// <param name="sessionId">The leased session.</param>
    /// <param name="ownerId">The owner that holds the lease.</param>
    public DragonflySessionRunLease(IDatabase database, string sessionId, string ownerId)
    {
        _database = database;
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
    public async Task<bool> RenewAsync(TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(timeToLive, DragonflySessionRunLeases.MinimumTimeToLive);
        cancellationToken.ThrowIfCancellationRequested();

        var renewed = await _database.ScriptEvaluateAsync(
            DragonflyScripts.RenewLease,
            new[] { DragonflyKeys.RunLease(SessionId) },
            new RedisValue[] { OwnerId, (long)timeToLive.TotalMilliseconds }).ConfigureAwait(false);
        return (long)renewed == 1;
    }

    /// <summary>Releases the lease, but only while this owner still holds it.</summary>
    /// <returns>A task that completes when the release has been applied.</returns>
    public async ValueTask DisposeAsync()
    {
        await _database.ScriptEvaluateAsync(
            DragonflyScripts.DeleteIfValue,
            new[] { DragonflyKeys.RunLease(SessionId) },
            new RedisValue[] { OwnerId }).ConfigureAwait(false);
    }
}

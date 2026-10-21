using StackExchange.Redis;

namespace OmpOS.Coordination.Dragonfly;

/// <summary>
/// <see cref="ISessionRunLeases"/> backed by Dragonfly. Acquisition is <c>SET NX PX</c>. Renewal and release are Lua
/// compare-and-set scripts, so a stale owner can never extend or delete a lease that another owner holds.
/// </summary>
public sealed class DragonflySessionRunLeases : ISessionRunLeases
{
    /// <summary>The shortest lease lifetime accepted. Dragonfly expresses lifetimes in whole milliseconds.</summary>
    internal static readonly TimeSpan MinimumTimeToLive = TimeSpan.FromMilliseconds(1);

    private readonly IDatabase _database;

    /// <summary>Creates a lease store over the database of <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    public DragonflySessionRunLeases(IConnectionMultiplexer multiplexer)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _database = multiplexer.GetDatabase();
    }

    /// <summary>Attempts to take the exclusive run lease for a session.</summary>
    /// <param name="sessionId">The session to lease.</param>
    /// <param name="ownerId">The identity of the caller that will hold the lease.</param>
    /// <param name="timeToLive">How long the lease lasts unless renewed.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    /// <returns>The acquired lease, or <see langword="null"/> when another owner holds an unexpired lease.</returns>
    public async Task<ISessionRunLease?> TryAcquireAsync(string sessionId, string ownerId, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeToLive, MinimumTimeToLive);
        cancellationToken.ThrowIfCancellationRequested();

        var acquired = await _database.StringSetAsync(DragonflyKeys.RunLease(sessionId), ownerId, timeToLive, when: When.NotExists).ConfigureAwait(false);
        return acquired ? new DragonflySessionRunLease(_database, sessionId, ownerId) : null;
    }

    /// <summary>Reports whether an unexpired lease exists for a session, whoever holds it.</summary>
    /// <param name="sessionId">The session to inspect.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    /// <returns><see langword="true"/> when a live lease exists.</returns>
    public async Task<bool> IsHeldAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        return await _database.KeyExistsAsync(DragonflyKeys.RunLease(sessionId)).ConfigureAwait(false);
    }
}

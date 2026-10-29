using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// <see cref="ISessionRunLeases"/> backed by Dragonfly. Acquisition is <c>SET NX PX</c>. Renewal and release are Lua
/// compare-and-set scripts, so a stale owner can never extend or delete a lease that another owner holds.
/// </summary>
public sealed class DragonflySessionRunLeases : ISessionRunLeases
{
    /// <summary>The shortest lease lifetime accepted. Dragonfly expresses lifetimes in whole milliseconds.</summary>
    internal static readonly TimeSpan MinimumTimeToLive = TimeSpan.FromMilliseconds(1);

    private readonly IDatabase _database;
    private readonly DragonflyResilience _resilience;

    /// <summary>Creates a lease store over the database of <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    /// <param name="resilience">The pipeline of the connection. Null builds one from the defaults.</param>
    public DragonflySessionRunLeases(IConnectionMultiplexer multiplexer, DragonflyResilience? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _database = multiplexer.GetDatabase();
        _resilience = resilience ?? DragonflyResilience.Create();
    }

    /// <summary>Attempts to take the exclusive run lease for a session.</summary>
    /// <param name="sessionId">The session to lease.</param>
    /// <param name="ownerId">The identity of the caller that will hold the lease.</param>
    /// <param name="timeToLive">How long the lease lasts unless renewed.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    /// <returns>The acquired lease, or <see langword="null"/> when another owner holds an unexpired lease.</returns>
    /// <exception cref="Autonogrammer.Resilience.DataPlaneTimeoutException">Dragonfly could not answer: the run is refused because it could double-execute.</exception>
    public async Task<ISessionRunLease?> TryAcquireAsync(string sessionId, string ownerId, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentOutOfRangeException.ThrowIfLessThan(timeToLive, MinimumTimeToLive);
        _ = DragonflyKeyspace.RequireTimeToLive(timeToLive);
        cancellationToken.ThrowIfCancellationRequested();

        var key = DragonflyKeyspace.LeaseRun(sessionId);
        // Not idempotent: a retry after a lost reply would report a lease this owner already holds as taken.
        var acquired = await _resilience.ClosedAsync(false, () => _database.StringSetAsync(key, ownerId, timeToLive, when: When.NotExists), cancellationToken).ConfigureAwait(false);
        return acquired ? new DragonflySessionRunLease(_database, _resilience, sessionId, ownerId) : null;
    }

    /// <summary>Reports whether an unexpired lease exists for a session, whoever holds it.</summary>
    /// <param name="sessionId">The session to inspect.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    /// <returns><see langword="true"/> when a live lease exists.</returns>
    public async Task<bool> IsHeldAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        var key = DragonflyKeyspace.LeaseRun(sessionId);
        return await _resilience.ClosedAsync(true, () => _database.KeyExistsAsync(key), cancellationToken).ConfigureAwait(false);
    }
}

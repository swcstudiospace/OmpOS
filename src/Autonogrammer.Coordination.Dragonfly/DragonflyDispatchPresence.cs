using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// <see cref="IDispatchPresence"/> backed by one Dragonfly key per device. The key is built only by
/// <see cref="DragonflyKeyspace.Presence"/> from the tenant hash of <see cref="ITenantHasher"/>. The value is the holder.
/// The lifetime is written in the same command as the value. A refresh is a compare-and-set that declares its only key in
/// <c>KEYS</c>. Reads and writes are open: an unreachable Dragonfly is unknown, never offline, and never thrown.
/// </summary>
public sealed class DragonflyDispatchPresence : IDispatchPresence
{
    private readonly IDatabase _database;
    private readonly ITenantHasher _tenantHasher;
    private readonly DragonflyResilience _resilience;

    /// <summary>Creates a presence store over the database of <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    /// <param name="tenantHasher">Hashes the account id into the tenant segment of the key.</param>
    /// <param name="resilience">The pipeline of the connection. Null builds one from the defaults.</param>
    public DragonflyDispatchPresence(IConnectionMultiplexer multiplexer, ITenantHasher tenantHasher, DragonflyResilience? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        ArgumentNullException.ThrowIfNull(tenantHasher);
        _database = multiplexer.GetDatabase();
        _tenantHasher = tenantHasher;
        _resilience = resilience ?? DragonflyResilience.Create();
    }

    /// <inheritdoc />
    public async Task SetAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(holder);
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key(accountId, deviceId);
        var lifetime = DragonflyKeyspace.RequireTimeToLive(timeToLive);

        // SET with PX is one command. A later EXPIRE would leave a key with no lifetime if the second command was lost.
        _ = await _resilience.TryOpenAsync(
            () => _database.StringSetAsync(key, holder, lifetime),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> RefreshAsync(string accountId, string deviceId, string holder, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(holder);
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key(accountId, deviceId);
        var lifetime = DragonflyKeyspace.RequireTimeToLive(timeToLive);
        long renewed = 0;
        var answered = await _resilience.TryOpenAsync(
            async () =>
            {
                var result = await _database.ScriptEvaluateAsync(
                    DragonflyScripts.RenewLease,
                    [key],
                    [holder, (long)lifetime.TotalMilliseconds]).ConfigureAwait(false);
                renewed = (long)result;
            },
            cancellationToken).ConfigureAwait(false);
        return answered && renewed == 1;
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string accountId, string deviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key(accountId, deviceId);
        _ = await _resilience.TryOpenAsync(() => _database.KeyDeleteAsync(key), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DispatchPresence> GetAsync(string accountId, string deviceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = Key(accountId, deviceId);
        RedisValue stored = default;
        var answered = false;
        var reached = await _resilience.TryOpenAsync(
            async () =>
            {
                stored = await _database.StringGetAsync(key).ConfigureAwait(false);
                answered = true;
            },
            cancellationToken).ConfigureAwait(false);
        if (!reached || !answered)
        {
            return DispatchPresence.Unknown;
        }

        return stored.IsNullOrEmpty ? DispatchPresence.Offline : DispatchPresence.Online(stored.ToString());
    }

    private RedisKey Key(string accountId, string deviceId)
    {
        return DragonflyKeyspace.Presence(_tenantHasher.HashTenant(accountId), deviceId);
    }
}

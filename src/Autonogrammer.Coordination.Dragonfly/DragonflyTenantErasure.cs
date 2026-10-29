using Autonogrammer.Resilience;
using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// <see cref="ITenantKeyEraser"/> for Dragonfly. It runs the only scan the keyspace allows, <c>SCAN MATCH autonogrammer:v1:*:{tenant}:*</c>, and deletes
/// what it finds. Only keys that carry the tenant segment match (presence and rate-limit keys). Lease, idempotency and nonce keys are addressed by
/// session id or by hash, hold only reconstructible state and expire on their own within 24 hours, so a tenant scan cannot and need not reach them.
/// </summary>
public sealed class DragonflyTenantErasure : ITenantKeyEraser
{
    private const int PageSize = 500;

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly DragonflyResilience _resilience;

    /// <summary>Creates an eraser over <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    /// <param name="resilience">The pipeline of the connection. Null builds one from the defaults.</param>
    public DragonflyTenantErasure(IConnectionMultiplexer multiplexer, DragonflyResilience? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _multiplexer = multiplexer;
        _resilience = resilience ?? DragonflyResilience.Create();
    }

    /// <summary>Deletes every key that carries the tenant segment.</summary>
    /// <param name="tenantHash">The tenant segment: 32 lowercase hexadecimal characters. Anything else throws before a command is sent.</param>
    /// <param name="cancellationToken">Cancels the erasure.</param>
    /// <returns>A task that completes when the keys are deleted.</returns>
    /// <exception cref="ArgumentException">The tenant segment is malformed.</exception>
    /// <exception cref="DataPlaneTimeoutException">Dragonfly could not answer.</exception>
    public async Task DeleteTenantKeysAsync(string tenantHash, CancellationToken cancellationToken)
    {
        var pattern = DragonflyKeyspace.TenantScanPattern(tenantHash);
        cancellationToken.ThrowIfCancellationRequested();

        var database = _multiplexer.GetDatabase();
        try
        {
            foreach (var endpoint in _multiplexer.GetEndPoints())
            {
                var server = _multiplexer.GetServer(endpoint);
                if (server.IsReplica)
                {
                    continue;
                }

                var batch = new List<RedisKey>(PageSize);
                await foreach (var key in server.KeysAsync(database.Database, pattern, PageSize).WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    batch.Add(key);
                    if (batch.Count >= PageSize)
                    {
                        await DeleteAsync(database, batch, cancellationToken).ConfigureAwait(false);
                    }
                }

                await DeleteAsync(database, batch, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (DragonflyResilience.IsOutage(exception))
        {
            throw new DataPlaneTimeoutException(_resilience.Pipeline.Component, DataPlaneTimeoutReason.Timeout);
        }
    }

    private async Task DeleteAsync(IDatabase database, List<RedisKey> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        var keys = batch.ToArray();
        batch.Clear();
        await _resilience.ClosedAsync(true, () => database.KeyDeleteAsync(keys), cancellationToken).ConfigureAwait(false);
    }
}

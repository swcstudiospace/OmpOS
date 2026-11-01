using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// <see cref="IDispatchAuthNonces"/> backed by Dragonfly. A claim is <c>SET NX PX</c> on the key built only by
/// <see cref="DragonflyKeyspace.AuthNonce"/>. A consume is a delete that reports whether the key existed. Both fail closed
/// when Dragonfly cannot answer. The nonce and its hash are never logged.
/// </summary>
public sealed class DragonflyDispatchAuthNonces : IDispatchAuthNonces
{
    private const string StoredMark = "1";

    private readonly IDatabase _database;
    private readonly DragonflyResilience _resilience;

    /// <summary>Creates a nonce store over the database of <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    /// <param name="resilience">The pipeline of the connection. Null builds one from the defaults.</param>
    public DragonflyDispatchAuthNonces(IConnectionMultiplexer multiplexer, DragonflyResilience? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _database = multiplexer.GetDatabase();
        _resilience = resilience ?? DragonflyResilience.Create();
    }

    /// <inheritdoc />
    /// <exception cref="Autonogrammer.Resilience.DataPlaneTimeoutException">Dragonfly could not answer. The hub refuses the hello.</exception>
    public async Task<bool> TryClaimAsync(string nonceSha256Hex, TimeSpan timeToLive, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = DragonflyKeyspace.AuthNonce(nonceSha256Hex);
        var lifetime = DragonflyKeyspace.RequireTimeToLive(timeToLive);

        // SET NX is not idempotent: a retry after a lost reply would report a claim this caller already holds as taken.
        // PX is part of the same command, so the key cannot be left without a lifetime.
        return await _resilience.ClosedAsync(
            false,
            () => _database.StringSetAsync(key, StoredMark, lifetime, when: When.NotExists),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="Autonogrammer.Resilience.DataPlaneTimeoutException">Dragonfly could not answer. The hub refuses the hello.</exception>
    public async Task<bool> TryConsumeAsync(string nonceSha256Hex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = DragonflyKeyspace.AuthNonce(nonceSha256Hex);

        // GETDEL is not idempotent: a retry after a lost reply would report a nonce this caller already consumed as absent.
        var stored = await _resilience.ClosedAsync(
            false,
            () => _database.StringGetDeleteAsync(key),
            cancellationToken).ConfigureAwait(false);
        return stored.HasValue;
    }
}

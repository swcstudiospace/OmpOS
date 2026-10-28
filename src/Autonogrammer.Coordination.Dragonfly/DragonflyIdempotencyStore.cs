using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// <see cref="IIdempotencyStore"/> backed by Dragonfly. A claim is <c>SET NX</c> with the pending lifetime. When the key
/// already exists, the stored value decides the outcome. The fingerprint is part of the stored value, so it must not
/// contain '|'.
/// </summary>
public sealed class DragonflyIdempotencyStore : IIdempotencyStore
{
    /// <summary>How many times a claim retries when a record expires between its <c>SET NX</c> and its <c>GET</c>.</summary>
    private const int MaxClaimAttempts = 3;

    private readonly IDatabase _database;

    /// <summary>Creates a store over the database of <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    public DragonflyIdempotencyStore(IConnectionMultiplexer multiplexer)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _database = multiplexer.GetDatabase();
    }

    /// <summary>Claims a key in a scope for a request with the given fingerprint.</summary>
    /// <param name="scope">The namespace of the key.</param>
    /// <param name="key">The client-supplied idempotency key.</param>
    /// <param name="fingerprint">A digest of the request. It must not contain '|'.</param>
    /// <param name="cancellationToken">Cancels the request before the first command is sent.</param>
    /// <returns>The claim outcome.</returns>
    /// <exception cref="InvalidOperationException">The stored record was malformed, or it expired during every claim attempt.</exception>
    public async Task<IdempotencyDecision> ClaimAsync(string scope, string key, string fingerprint, CancellationToken cancellationToken)
    {
        ValidateArguments(scope, key, fingerprint);
        cancellationToken.ThrowIfCancellationRequested();

        var redisKey = DragonflyKeys.Idempotency(scope, key);
        for (var attempt = 0; attempt < MaxClaimAttempts; attempt++)
        {
            var claimed = await _database.StringSetAsync(
                redisKey,
                DragonflyKeys.PendingValue(fingerprint),
                CoordinationDefaults.IdempotencyPendingTimeToLive,
                when: When.NotExists).ConfigureAwait(false);
            if (claimed)
            {
                return new IdempotencyDecision(IdempotencyOutcome.Claimed, null);
            }

            var stored = await _database.StringGetAsync(redisKey).ConfigureAwait(false);
            if (stored.HasValue)
            {
                return Interpret(stored.ToString(), fingerprint);
            }

            // The record expired between the failed SET NX and the GET, so the next SET NX can claim the key.
        }

        throw new InvalidOperationException("The idempotency key expired during every claim attempt.");
    }

    /// <summary>Stores the response for a claimed key, but only while that key still holds this request's pending claim.</summary>
    /// <param name="scope">The namespace of the key.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="fingerprint">The fingerprint the claim was made with. It must not contain '|'.</param>
    /// <param name="responseJson">The response to replay.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    /// <returns>A task that completes when the compare-and-set has run. It does nothing when the pending claim is no longer present.</returns>
    public async Task CompleteAsync(string scope, string key, string fingerprint, string responseJson, CancellationToken cancellationToken)
    {
        ValidateArguments(scope, key, fingerprint);
        ArgumentNullException.ThrowIfNull(responseJson);
        cancellationToken.ThrowIfCancellationRequested();

        await _database.ScriptEvaluateAsync(
            DragonflyScripts.CompleteIfPending,
            new[] { DragonflyKeys.Idempotency(scope, key) },
            new RedisValue[]
            {
                DragonflyKeys.PendingValue(fingerprint),
                DragonflyKeys.CompletedValue(fingerprint, responseJson),
                (long)CoordinationDefaults.IdempotencyCompletedTimeToLive.TotalMilliseconds,
            }).ConfigureAwait(false);
    }

    /// <summary>Drops a pending claim made with the same fingerprint. Completed records are never removed.</summary>
    /// <param name="scope">The namespace of the key.</param>
    /// <param name="key">The idempotency key.</param>
    /// <param name="fingerprint">The fingerprint the claim was made with. It must not contain '|'.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    /// <returns>A task that completes when the release has been applied.</returns>
    public async Task ReleaseAsync(string scope, string key, string fingerprint, CancellationToken cancellationToken)
    {
        ValidateArguments(scope, key, fingerprint);
        cancellationToken.ThrowIfCancellationRequested();

        await _database.ScriptEvaluateAsync(
            DragonflyScripts.DeleteIfValue,
            new[] { DragonflyKeys.Idempotency(scope, key) },
            new RedisValue[] { DragonflyKeys.PendingValue(fingerprint) }).ConfigureAwait(false);
    }

    private static IdempotencyDecision Interpret(string stored, string fingerprint)
    {
        if (stored.StartsWith(DragonflyKeys.PendingPrefix, StringComparison.Ordinal))
        {
            var pendingFingerprint = stored[DragonflyKeys.PendingPrefix.Length..];
            return string.Equals(pendingFingerprint, fingerprint, StringComparison.Ordinal)
                ? new IdempotencyDecision(IdempotencyOutcome.InProgress, null)
                : new IdempotencyDecision(IdempotencyOutcome.Conflict, null);
        }

        if (stored.StartsWith(DragonflyKeys.CompletedPrefix, StringComparison.Ordinal))
        {
            var body = stored[DragonflyKeys.CompletedPrefix.Length..];
            var separator = body.IndexOf('|', StringComparison.Ordinal);
            if (separator < 0)
            {
                throw new InvalidOperationException("The stored idempotency record is malformed.");
            }

            var completedFingerprint = body[..separator];
            var responseJson = body[(separator + 1)..];
            return string.Equals(completedFingerprint, fingerprint, StringComparison.Ordinal)
                ? new IdempotencyDecision(IdempotencyOutcome.Replay, responseJson)
                : new IdempotencyDecision(IdempotencyOutcome.Conflict, null);
        }

        throw new InvalidOperationException("The stored idempotency record is malformed.");
    }

    private static void ValidateArguments(string scope, string key, string fingerprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        if (fingerprint.Contains('|', StringComparison.Ordinal))
        {
            throw new ArgumentException("The fingerprint must not contain '|'.", nameof(fingerprint));
        }
    }
}

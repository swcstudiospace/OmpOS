using OmpOS.Contracts.Cloud;

namespace OmpOS.Api.Cloud;

/// <summary>
/// The in-process <see cref="ICloudAgentStore"/>: one lock guards every operation, so each is atomic. It is the default for a host
/// whose <c>Sessions:Store</c> is not Postgres, and for tests. Records are lost on restart and not shared between instances.
/// </summary>
internal sealed class InMemoryCloudAgentStore : ICloudAgentStore
{
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<string, CloudAgentRecord> _records = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<CloudAgentCreateResult> CreateAsync(CloudAgentRecord record, CloudAgentLimits limits, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(limits);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (record.IdempotencyKey is { } key)
            {
                var existing = _records.Values.FirstOrDefault(candidate =>
                    string.Equals(candidate.OwnerId, record.OwnerId, StringComparison.Ordinal)
                    && string.Equals(candidate.IdempotencyKey, key, StringComparison.Ordinal));
                if (existing is not null)
                {
                    return Task.FromResult(string.Equals(existing.RequestFingerprint, record.RequestFingerprint, StringComparison.Ordinal)
                        ? new CloudAgentCreateResult(CloudAgentCreateOutcome.Replayed, existing)
                        : new CloudAgentCreateResult(CloudAgentCreateOutcome.IdempotencyKeyReused, null));
                }
            }

            var owned = _records.Values.Where(candidate => string.Equals(candidate.OwnerId, record.OwnerId, StringComparison.Ordinal)).ToList();
            if (owned.Count >= limits.MaxAgents)
            {
                return Task.FromResult(new CloudAgentCreateResult(CloudAgentCreateOutcome.AgentLimitReached, null));
            }

            if (owned.Count(static candidate => CloudAgentStatusRules.IsActive(candidate.Status)) >= limits.MaxActive)
            {
                return Task.FromResult(new CloudAgentCreateResult(CloudAgentCreateOutcome.ActiveLimitReached, null));
            }

            if (!_records.TryAdd(record.Id, record))
            {
                throw new InvalidOperationException($"A cloud agent with id '{record.Id}' already exists.");
            }

            return Task.FromResult(new CloudAgentCreateResult(CloudAgentCreateOutcome.Created, record));
        }
    }

    /// <inheritdoc />
    public Task<CloudAgentRecord?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            return Task.FromResult(_records.GetValueOrDefault(id));
        }
    }

    /// <inheritdoc />
    public Task<CloudAgentRecord?> GetBySessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        lock (_gate)
        {
            return Task.FromResult(_records.Values.FirstOrDefault(candidate => string.Equals(candidate.SessionId, sessionId, StringComparison.Ordinal)));
        }
    }

    /// <inheritdoc />
    public Task<CloudAgentRecord?> FindByIdempotencyKeyAsync(string ownerId, string idempotencyKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        lock (_gate)
        {
            return Task.FromResult(_records.Values.FirstOrDefault(candidate =>
                string.Equals(candidate.OwnerId, ownerId, StringComparison.Ordinal)
                && string.Equals(candidate.IdempotencyKey, idempotencyKey, StringComparison.Ordinal)));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudAgentRecord>> ListByOwnerAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        lock (_gate)
        {
            var found = _records.Values
                .Where(candidate => string.Equals(candidate.OwnerId, ownerId, StringComparison.Ordinal))
                .OrderByDescending(static candidate => candidate.CreatedAt)
                .ThenBy(static candidate => candidate.Id, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult<IReadOnlyList<CloudAgentRecord>>(found);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudAgentRecord>> ListByStatusAsync(IReadOnlyCollection<CloudAgentStatus> statuses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statuses);
        lock (_gate)
        {
            var found = _records.Values
                .Where(candidate => statuses.Contains(candidate.Status))
                .OrderBy(static candidate => candidate.UpdatedAt)
                .ThenBy(static candidate => candidate.Id, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult<IReadOnlyList<CloudAgentRecord>>(found);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudAgentRecord>> ListAllAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var found = _records.Values
                .OrderBy(static candidate => candidate.CreatedAt)
                .ThenBy(static candidate => candidate.Id, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult<IReadOnlyList<CloudAgentRecord>>(found);
        }
    }

    /// <inheritdoc />
    public Task<CloudAgentRecord?> UpdateAsync(string id, Func<CloudAgentRecord, CloudAgentRecord> mutate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(mutate);
        lock (_gate)
        {
            if (!_records.TryGetValue(id, out var current))
            {
                return Task.FromResult<CloudAgentRecord?>(null);
            }

            var updated = mutate(current);
            if (!string.Equals(updated.Id, current.Id, StringComparison.Ordinal)
                || !string.Equals(updated.OwnerId, current.OwnerId, StringComparison.Ordinal)
                || !string.Equals(updated.SessionId, current.SessionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A cloud agent update must not change the identifier, the owner or the session.");
            }

            _records[id] = updated;
            return Task.FromResult<CloudAgentRecord?>(updated);
        }
    }

    /// <inheritdoc />
    public Task<CloudAgentActivation> TryActivateAsync(string id, int maxActive, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            if (!_records.TryGetValue(id, out var current))
            {
                return Task.FromResult(new CloudAgentActivation(CloudAgentActivationOutcome.NotFound, null));
            }

            if (CloudAgentStatusRules.IsActive(current.Status))
            {
                return Task.FromResult(new CloudAgentActivation(CloudAgentActivationOutcome.AlreadyActive, current));
            }

            if (!CloudAgentStatusRules.CanActivate(current.Status))
            {
                return Task.FromResult(new CloudAgentActivation(CloudAgentActivationOutcome.NotWakeable, current));
            }

            var active = _records.Values.Count(candidate =>
                string.Equals(candidate.OwnerId, current.OwnerId, StringComparison.Ordinal) && CloudAgentStatusRules.IsActive(candidate.Status));
            if (active >= maxActive)
            {
                return Task.FromResult(new CloudAgentActivation(CloudAgentActivationOutcome.LimitReached, current));
            }

            var moved = CloudAgentStatusRules.Activate(current, now);
            _records[id] = moved;
            return Task.FromResult(new CloudAgentActivation(CloudAgentActivationOutcome.Activated, moved));
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            return Task.FromResult(_records.Remove(id));
        }
    }

    /// <inheritdoc />
    public Task<CloudAgentUsage> GetUsageAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        lock (_gate)
        {
            var owned = _records.Values.Where(candidate => string.Equals(candidate.OwnerId, ownerId, StringComparison.Ordinal)).ToList();
            return Task.FromResult(new CloudAgentUsage(
                owned.Count,
                owned.Count(static candidate => CloudAgentStatusRules.IsActive(candidate.Status)),
                owned.Sum(static candidate => candidate.ComputeSeconds)));
        }
    }
}

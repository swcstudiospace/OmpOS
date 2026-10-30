using Autonogrammer.Contracts.Cloud;

namespace Autonogrammer.Api.Cloud;

/// <summary>
/// An in-process <see cref="ICloudSecretStore"/> and <see cref="ICloudSecretProvider"/>. Values live in memory only: they are lost on restart, not
/// shared between instances, and not encrypted (process memory is the protection). A multi-instance host needs an operator-supplied persistent,
/// encrypted implementation of the two interfaces.
/// </summary>
internal sealed class InMemoryCloudSecretStore : ICloudSecretStore, ICloudSecretProvider
{
    private readonly TimeProvider _time;
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<(string Owner, string Name), Entry> _entries = [];

    /// <summary>Initializes a new instance of the <see cref="InMemoryCloudSecretStore"/> class.</summary>
    /// <param name="time">The clock that stamps writes.</param>
    public InMemoryCloudSecretStore(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        _time = time;
    }

    /// <inheritdoc />
    public Task<CloudSecretInfo?> SetAsync(string ownerId, string name, string value, int maxSecrets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            var key = (ownerId, name);
            if (!_entries.ContainsKey(key) && _entries.Keys.Count(owned => string.Equals(owned.Owner, ownerId, StringComparison.Ordinal)) >= maxSecrets)
            {
                return Task.FromResult<CloudSecretInfo?>(null);
            }

            var updatedAt = _time.GetUtcNow();
            _entries[key] = new Entry(value, updatedAt);
            return Task.FromResult<CloudSecretInfo?>(new CloudSecretInfo { Name = name, UpdatedAt = updatedAt });
        }
    }

    /// <inheritdoc />
    public Task<CloudSecretInfo?> GetInfoAsync(string ownerId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return Task.FromResult(_entries.TryGetValue((ownerId, name), out var entry)
                ? new CloudSecretInfo { Name = name, UpdatedAt = entry.UpdatedAt }
                : null);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CloudSecretInfo>> ListAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        lock (_gate)
        {
            var found = _entries
                .Where(pair => string.Equals(pair.Key.Owner, ownerId, StringComparison.Ordinal))
                .OrderBy(static pair => pair.Key.Name, StringComparer.Ordinal)
                .Select(static pair => new CloudSecretInfo { Name = pair.Key.Name, UpdatedAt = pair.Value.UpdatedAt })
                .ToList();
            return Task.FromResult<IReadOnlyList<CloudSecretInfo>>(found);
        }
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string ownerId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return Task.FromResult(_entries.Remove((ownerId, name)));
        }
    }

    /// <inheritdoc />
    public ValueTask<string?> TryGetValueAsync(string ownerId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return ValueTask.FromResult(_entries.TryGetValue((ownerId, name), out var entry) ? entry.Value : null);
        }
    }

    /// <inheritdoc />
    public ValueTask<bool> ExistsAsync(string ownerId, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(name);
        lock (_gate)
        {
            return ValueTask.FromResult(_entries.ContainsKey((ownerId, name)));
        }
    }

    /// <summary>One stored secret. <see cref="ToString"/> never prints the value.</summary>
    /// <param name="Value">The secret value.</param>
    /// <param name="UpdatedAt">When it was written.</param>
    private sealed record Entry(string Value, DateTimeOffset UpdatedAt)
    {
        /// <inheritdoc />
        public override string ToString() => "Entry (value not shown)";
    }
}

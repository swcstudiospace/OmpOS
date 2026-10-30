using System.Collections.Concurrent;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>
/// Remembers which caller added which custom endpoint, so a caller lists, replaces and removes only their own. It lives in memory like the
/// session registries: a restart forgets it, and a block written before the restart stays in omp's configuration until the operator removes it.
/// </summary>
internal sealed class CustomProviderLedger
{
    private readonly ConcurrentDictionary<string, CustomProviderRecord> _records = new(StringComparer.Ordinal);

    /// <summary>Finds a record by provider id, whoever owns it.</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>The record, or null.</returns>
    public CustomProviderRecord? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _records.GetValueOrDefault(id);
    }

    /// <summary>Lists the records of one caller, ordered by id.</summary>
    /// <param name="owner">The caller.</param>
    /// <returns>The records.</returns>
    public IReadOnlyList<CustomProviderRecord> ListFor(string owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return [.. _records.Values.Where(record => string.Equals(record.Owner, owner, StringComparison.Ordinal)).OrderBy(static record => record.Id, StringComparer.Ordinal)];
    }

    /// <summary>Stores a record, replacing one the same caller added under the same id.</summary>
    /// <param name="record">The record.</param>
    /// <returns>False when another caller owns the id.</returns>
    public bool TrySet(CustomProviderRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        while (true)
        {
            if (_records.TryAdd(record.Id, record))
            {
                return true;
            }

            if (!_records.TryGetValue(record.Id, out var existing))
            {
                continue;
            }

            if (!string.Equals(existing.Owner, record.Owner, StringComparison.Ordinal))
            {
                return false;
            }

            if (_records.TryUpdate(record.Id, record, existing))
            {
                return true;
            }
        }
    }

    /// <summary>Removes a record of one caller.</summary>
    /// <param name="owner">The caller.</param>
    /// <param name="id">The provider id.</param>
    /// <returns>True when the caller had such a record.</returns>
    public bool TryRemove(string owner, string id)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(id);
        return _records.TryGetValue(id, out var existing)
            && string.Equals(existing.Owner, owner, StringComparison.Ordinal)
            && _records.TryRemove(new KeyValuePair<string, CustomProviderRecord>(id, existing));
    }
}

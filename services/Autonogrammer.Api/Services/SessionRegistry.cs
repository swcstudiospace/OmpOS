using System.Collections.Concurrent;

namespace Autonogrammer.Api.Services;

/// <summary>
/// Index of session identifiers created through <c>POST /sessions</c>. The session store
/// has no list operation, so the API keeps the id set and hydrates entries on read.
/// </summary>
internal sealed class SessionRegistry
{
    private readonly ConcurrentDictionary<string, byte> _ids = new(StringComparer.Ordinal);

    /// <summary>Adds a session identifier to the index.</summary>
    /// <param name="id">The session identifier.</param>
    public void Add(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        _ids.TryAdd(id, 0);
    }

    /// <summary>Gets a snapshot of the indexed identifiers.</summary>
    public IReadOnlyCollection<string> Ids => [.. _ids.Keys];
}

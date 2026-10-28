using OmpOS.Contracts.Agents;
using OmpOS.Contracts.Catalog;
using OmpOS.Presentation.Kanban;

namespace OmpOS.Web.Services;

/// <summary>
/// The <see cref="ISessionCatalog"/> of the web head: the rows <c>GET /sessions</c> returned, kept current by the user event stream. It
/// raises <see cref="Changed"/> after every change. It is used on the one UI context of the browser and is not thread safe.
/// </summary>
internal sealed class WebSessionCatalog : ISessionCatalog
{
    private readonly Dictionary<string, SessionCatalogEntry> _entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Gets the rows, most recently active first.</summary>
    internal IReadOnlyList<SessionCatalogEntry> Entries =>
        [.. _entries.Values.OrderByDescending(static entry => entry.LastActivityAt).ThenBy(static entry => entry.Id, StringComparer.Ordinal)];

    /// <inheritdoc />
    public IReadOnlyList<SessionSummary> Snapshot() => [.. Entries.Select(CatalogMapper.ToSummary)];

    /// <summary>Gets one row.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The row, or null.</returns>
    internal SessionCatalogEntry? Find(string sessionId) => _entries.GetValueOrDefault(sessionId);

    /// <summary>Gets the live runtime status of a session, for <c>SessionCatalogOptions.LiveStatus</c>.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The status, or null.</returns>
    internal RuntimeStatus? LiveStatusOf(string sessionId) =>
        _entries.TryGetValue(sessionId, out var entry) ? CatalogMapper.LiveStatusOf(entry.RowStatus) : null;

    /// <summary>Gets whether the session is closed, for <c>SessionCatalogOptions.IsClosed</c>.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>True when the API reports the session closed.</returns>
    internal bool IsClosed(string sessionId) => _entries.TryGetValue(sessionId, out var entry) && entry.Closed;

    /// <summary>Replaces every row with the answer of <c>GET /sessions</c>.</summary>
    /// <param name="entries">The rows.</param>
    internal void Replace(IEnumerable<SessionCatalogEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries.Clear();
        foreach (var entry in entries)
        {
            _entries[entry.Id] = entry;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds or replaces one row.</summary>
    /// <param name="entry">The row.</param>
    internal void Upsert(SessionCatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries[entry.Id] = entry;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Applies a <c>session_status</c> event to its row.</summary>
    /// <param name="status">The event.</param>
    /// <returns>False when the session is not in the catalogue yet, so the caller reloads it.</returns>
    internal bool Apply(SessionStatusEvent status)
    {
        ArgumentNullException.ThrowIfNull(status);
        if (!_entries.TryGetValue(status.SessionId, out var entry))
        {
            return false;
        }

        _entries[status.SessionId] = entry with
        {
            Status = status.Status,
            RowStatus = status.RowStatus,
            Group = status.Group,
            LastActivityAt = status.LastActivityAt,
        };
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Applies a <c>catalog_changed</c> event to its row.</summary>
    /// <param name="change">The event.</param>
    /// <returns>False when the session is not in the catalogue yet (a created session), so the caller reloads it.</returns>
    internal bool Apply(CatalogChangedEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (!_entries.TryGetValue(change.SessionId, out var entry))
        {
            return false;
        }

        _entries[change.SessionId] = entry with
        {
            RowStatus = change.RowStatus,
            Group = change.Group,
            Closed = change.Change switch
            {
                CatalogChangeKind.Closed => true,
                CatalogChangeKind.Reopened => false,
                _ => entry.Closed,
            },
        };
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Applies the answer of close, reopen or resume to its row.</summary>
    /// <param name="lifecycle">The response.</param>
    /// <returns>False when the session is not in the catalogue.</returns>
    internal bool Apply(SessionLifecycleResponse lifecycle)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        if (!_entries.TryGetValue(lifecycle.Id, out var entry))
        {
            return false;
        }

        _entries[lifecycle.Id] = entry with { Closed = lifecycle.Closed, RowStatus = lifecycle.RowStatus, Group = lifecycle.Group };
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}

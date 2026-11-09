using System.Runtime.InteropServices;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.Kanban;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The <see cref="ISessionCatalog"/> of the web head: the rows <c>GET /sessions</c> returned, kept current by the user event stream.
/// Changes are versioned so an older in-flight snapshot cannot overwrite a newer local or streamed update.
/// </summary>
internal sealed class WebSessionCatalog : ISessionCatalog
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, SessionCatalogEntry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EntryRevision> _entryRevisions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unconfirmedCreates = new(StringComparer.Ordinal);
    private long _revision;
    private long _nextSnapshot;
    private long _latestSnapshot;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Gets the rows, most recently active first.</summary>
    internal IReadOnlyList<SessionCatalogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries.Values.OrderByDescending(static entry => entry.LastActivityAt).ThenBy(static entry => entry.Id, StringComparer.Ordinal)];
            }
        }
    }

    /// <summary>Marks the start of a catalogue read so its eventual snapshot can be merged safely.</summary>
    internal SnapshotStamp BeginSnapshot()
    {
        lock (_gate)
        {
            return new SnapshotStamp(_revision, ++_nextSnapshot);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SessionSummary> Snapshot() => [.. Entries.Select(CatalogMapper.ToSummary)];

    /// <summary>Gets one row.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The row, or null.</returns>
    internal SessionCatalogEntry? Find(string sessionId)
    {
        lock (_gate)
        {
            return _entries.GetValueOrDefault(sessionId);
        }
    }

    /// <summary>Gets the live runtime status of a session, for <c>SessionCatalogOptions.LiveStatus</c>.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>The status, or null.</returns>
    internal RuntimeStatus? LiveStatusOf(string sessionId) =>
        Find(sessionId) is { } entry ? CatalogMapper.LiveStatusOf(entry.RowStatus) : null;

    /// <summary>Gets whether the session is closed, for <c>SessionCatalogOptions.IsClosed</c>.</summary>
    /// <param name="sessionId">The session.</param>
    /// <returns>True when the API reports the session closed.</returns>
    internal bool IsClosed(string sessionId) => Find(sessionId)?.Closed == true;

    /// <summary>Merges a snapshot without overwriting rows changed since its request began.</summary>
    /// <param name="entries">The rows from the server.</param>
    /// <param name="stamp">The stamp captured before the request.</param>
    /// <returns>False when this snapshot or a later-started one was already installed.</returns>
    internal bool MergeSnapshot(IReadOnlyList<SessionCatalogEntry> entries, SnapshotStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(entries);
        lock (_gate)
        {
            if (stamp.Sequence <= _latestSnapshot)
            {
                return false;
            }

            var seen = new HashSet<string>(entries.Count, StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                _ = seen.Add(entry.Id);
                if (!_entryRevisions.TryGetValue(entry.Id, out var revision) || revision.Last <= stamp.Revision)
                {
                    _ = _unconfirmedCreates.Remove(entry.Id);
                    _entries[entry.Id] = entry;
                    _ = _entryRevisions.Remove(entry.Id);
                }
                else if (revision.Created <= stamp.Revision)
                {
                    _ = _unconfirmedCreates.Remove(entry.Id);
                    var current = _entries[entry.Id];
                    _entries[entry.Id] = entry with
                    {
                        Status = revision.Status > stamp.Revision ? current.Status : entry.Status,
                        LastActivityAt = revision.Status > stamp.Revision ? current.LastActivityAt : entry.LastActivityAt,
                        RowStatus = revision.Classification > stamp.Revision ? current.RowStatus : entry.RowStatus,
                        Group = revision.Classification > stamp.Revision ? current.Group : entry.Group,
                        Closed = revision.Closed > stamp.Revision ? current.Closed : entry.Closed,
                    };
                }
            }

            List<string>? removed = null;
            foreach (var id in _entries.Keys)
            {
                if (!seen.Contains(id) && !_unconfirmedCreates.Contains(id)
                    && (!_entryRevisions.TryGetValue(id, out var revision) || revision.Last <= stamp.Revision))
                {
                    (removed ??= []).Add(id);
                }
            }

            if (removed is not null)
            {
                foreach (var id in removed)
                {
                    _ = _entries.Remove(id);
                    _ = _entryRevisions.Remove(id);
                }
            }

            _latestSnapshot = stamp.Sequence;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Preserves a freshly created session until a later snapshot confirms it.</summary>
    /// <param name="created">The created session.</param>
    internal void EnsureCreated(Session created)
    {
        ArgumentNullException.ThrowIfNull(created);
        var added = false;
        lock (_gate)
        {
            if (!_entries.ContainsKey(created.Id))
            {
                _entries.Add(created.Id, EntryOf(created));
                added = true;
            }

            var next = ++_revision;
            _entryRevisions[created.Id] = _entryRevisions.GetValueOrDefault(created.Id) with { Last = next, Created = next };
            _ = _unconfirmedCreates.Add(created.Id);
        }

        if (added)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Applies a <c>session_status</c> event to its row.</summary>
    /// <param name="status">The event.</param>
    /// <returns>False when the session is not in the catalogue yet, so the caller reloads it.</returns>
    internal bool Apply(SessionStatusEvent status)
    {
        ArgumentNullException.ThrowIfNull(status);
        lock (_gate)
        {
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
            var next = ++_revision;
            _entryRevisions[status.SessionId] = _entryRevisions.GetValueOrDefault(status.SessionId) with { Last = next, Status = next, Classification = next };
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Applies a <c>catalog_changed</c> event to its row.</summary>
    /// <param name="change">The event.</param>
    /// <returns>False when the session is not in the catalogue yet (a created session), so the caller reloads it.</returns>
    internal bool Apply(CatalogChangedEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
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
            var next = ++_revision;
            var revision = _entryRevisions.GetValueOrDefault(change.SessionId);
            _entryRevisions[change.SessionId] = revision with
            {
                Last = next,
                Classification = next,
                Closed = change.Change is CatalogChangeKind.Closed or CatalogChangeKind.Reopened ? next : revision.Closed,
            };
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Applies the answer of close, reopen or resume to its row.</summary>
    /// <param name="lifecycle">The response.</param>
    /// <returns>False when the session is not in the catalogue.</returns>
    internal bool Apply(SessionLifecycleResponse lifecycle)
    {
        ArgumentNullException.ThrowIfNull(lifecycle);
        lock (_gate)
        {
            if (!_entries.TryGetValue(lifecycle.Id, out var entry))
            {
                return false;
            }

            _entries[lifecycle.Id] = entry with { Closed = lifecycle.Closed, RowStatus = lifecycle.RowStatus, Group = lifecycle.Group };
            var next = ++_revision;
            _entryRevisions[lifecycle.Id] = _entryRevisions.GetValueOrDefault(lifecycle.Id) with { Last = next, Classification = next, Closed = next };
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private static SessionCatalogEntry EntryOf(Session created) => new()
    {
        Id = created.Id,
        OwnerId = created.OwnerId,
        CreatedAt = created.CreatedAt,
        UpdatedAt = created.UpdatedAt,
        Status = created.Status,
        SandboxId = created.SandboxId,
        Messages = created.Messages,
        Events = created.Events,
        Kind = CatalogSessionKind.Agent,
        RowStatus = CatalogRowStatus.Ready,
        Group = CatalogRowGroup.Idle,
        LastActivityAt = created.UpdatedAt,
        Closed = false,
    };

    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct SnapshotStamp(long Revision, long Sequence);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct EntryRevision(long Last, long Created, long Status, long Classification, long Closed);
}

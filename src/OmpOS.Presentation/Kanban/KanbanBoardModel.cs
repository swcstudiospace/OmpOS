using System.ComponentModel;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Kanban;

/// <summary>
/// The Kanban board: one task card per chat, placed in a column by its runtime status. The cards come from the rows of a
/// <see cref="SessionCatalogModel"/> (the single source of sessions, titles, last activity and the lifecycle-derived status); runtime
/// signals and heartbeat changes arrive through <see cref="Apply"/> and <see cref="SetStale"/>.
/// </summary>
/// <remarks>
/// <para>
/// Status comes only from runtime signals (VX-04): through <see cref="RuntimeStatusMap"/> for a signal, and through the catalog row's
/// <see cref="AgentRow.Card"/>, which the catalog derives with the same map. The board never sets a runtime status itself. A signal
/// that arrives between two catalog refreshes stays in force until the row's own status or stale flag changes, so a refresh that has
/// not yet seen the signal does not revert it.
/// </para>
/// <para>
/// Drag: only Backlog and Archived accept a drop (<see cref="KanbanColumn.AcceptsDrop"/>); a drop on a runtime column is refused and
/// changes nothing. A card the user placed stays in its column until its runtime status changes, then it returns to the column of the
/// new status. A chat without a runtime status sits in Backlog.
/// </para>
/// <para>Threading: every member runs on the context that owns the models; the catalog model already dispatches its own changes there.</para>
/// </remarks>
public sealed class KanbanBoardModel : ObservableModel, IDisposable
{
    private const int MaxTitleChars = 200;
    private const int MaxModelChars = 80;
    private const string StaleText = "Stale, no recent heartbeat";
    private const string UnreachableText = "Session unreachable";
    private const string NoStatusText = "No status";
    private const string ModelUnknownText = "Model unknown";

    private static readonly RuntimeStatus[] RuntimeOrder = Enum.GetValues<RuntimeStatus>();

    private readonly SessionCatalogModel _catalog;
    private readonly TimeProvider _timeProvider;
    private readonly KanbanBoardOptions _options;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private IReadOnlyList<KanbanColumn> _columns = [];
    private List<KanbanCard> _cards = [];
    private bool _disposed;

    /// <summary>Creates the board, reads the catalog's current rows and follows the catalog from then on.</summary>
    /// <param name="catalog">The catalog model whose rows are the chats of the board.</param>
    /// <param name="timeProvider">The clock of the relative times and of the last activity a signal records.</param>
    /// <param name="options">The head hooks. Null uses the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="timeProvider"/> is null.</exception>
    public KanbanBoardModel(SessionCatalogModel catalog, TimeProvider timeProvider, KanbanBoardOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _catalog = catalog;
        _timeProvider = timeProvider;
        _options = options ?? new KanbanBoardOptions();
        SyncEntries();
        Rebuild();
        _catalog.PropertyChanged += OnCatalogPropertyChanged;
    }

    /// <summary>Gets the columns in board order: Backlog, one per runtime status, Archived. Every column is present, empty or not.</summary>
    public IReadOnlyList<KanbanColumn> Columns => _columns;

    /// <summary>Gets every card, one per chat, most recent activity first.</summary>
    public IReadOnlyList<KanbanCard> Cards => _cards;

    /// <summary>Gets whether the board has no chat at all.</summary>
    public bool IsEmpty => _cards.Count == 0;

    /// <summary>Returns the card of a chat.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The card, or null when no chat has that identifier.</returns>
    public KanbanCard? CardOf(string sessionId) => _cards.FirstOrDefault(card => string.Equals(card.Id, sessionId, StringComparison.Ordinal));

    /// <summary>
    /// Applies a runtime signal to a chat: the status becomes the one <see cref="RuntimeStatusMap"/> gives the signal's kind, the stale flag is
    /// unchanged, and the last activity becomes now. A user placement of the card ends because its runtime status changed.
    /// </summary>
    /// <param name="sessionId">The session the signal belongs to.</param>
    /// <param name="signal">The signal.</param>
    /// <returns>True when the chat is on the board and the card changed; false when no chat has that identifier.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signal"/> is null.</exception>
    /// <exception cref="UnmappedRuntimeSignalException">The signal's kind has no status mapping; nothing changes.</exception>
    public bool Apply(string sessionId, RuntimeSignal signal)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(signal);
        var status = RuntimeStatusMap.StatusOf(signal.Kind);
        if (!_entries.TryGetValue(sessionId, out var entry))
        {
            return false;
        }

        entry.State = entry.State is { } state ? state.ApplySignal(signal) : new KanbanCardState(status, IsStale: false);
        entry.LastActivity = Latest(entry.LastActivity, _timeProvider.GetUtcNow());
        Rebuild();
        return true;
    }

    /// <summary>Sets the stale flag of a chat from its heartbeat. The status and the placement are unchanged.</summary>
    /// <param name="sessionId">The session whose heartbeat changed.</param>
    /// <param name="isStale">True when the heartbeat is overdue.</param>
    /// <returns>True when the chat is on the board and has a runtime status; false otherwise, with nothing changed.</returns>
    public bool SetStale(string sessionId, bool isStale)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        if (!_entries.TryGetValue(sessionId, out var entry) || entry.State is not { } state)
        {
            return false;
        }

        entry.State = state.WithStale(isStale);
        Rebuild();
        return true;
    }

    /// <summary>Returns whether a dragged card may be dropped on a column.</summary>
    /// <param name="columnKey">The column key.</param>
    /// <returns>True only for Backlog and Archived.</returns>
    public bool CanDrop(string columnKey) => _columns.FirstOrDefault(column => string.Equals(column.Key, columnKey, StringComparison.Ordinal))?.AcceptsDrop ?? false;

    /// <summary>Drops a card on a column. Only Backlog and Archived accept it; a runtime column refuses and nothing changes.</summary>
    /// <param name="sessionId">The card's session identifier.</param>
    /// <param name="columnKey">The key of the target column.</param>
    /// <returns>What happened.</returns>
    public KanbanMoveOutcome Move(string sessionId, string columnKey)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(columnKey);
        var column = _columns.FirstOrDefault(candidate => string.Equals(candidate.Key, columnKey, StringComparison.Ordinal));
        if (column is null)
        {
            return KanbanMoveOutcome.UnknownColumn;
        }

        if (!_entries.TryGetValue(sessionId, out var entry))
        {
            return KanbanMoveOutcome.UnknownCard;
        }

        if (column.UserStatus is not { } target)
        {
            return KanbanMoveOutcome.RefusedRuntimeColumn;
        }

        if (string.Equals(ColumnKeyOf(entry), column.Key, StringComparison.Ordinal))
        {
            return KanbanMoveOutcome.AlreadyThere;
        }

        entry.Placement = new Placement(target, entry.State?.Status);
        Rebuild();
        return KanbanMoveOutcome.Moved;
    }

    /// <summary>Re-derives the relative times from the clock, without reading the catalog again.</summary>
    public void RefreshTimes() => Rebuild();

    /// <summary>Stops following the catalog.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog.PropertyChanged -= OnCatalogPropertyChanged;
    }

    private static DateTimeOffset Latest(DateTimeOffset left, DateTimeOffset right) => left >= right ? left : right;

    private static string ColumnKeyOf(Entry entry) => entry.ActivePlacement is { } placement
        ? KanbanColumn.KeyOf(placement.Status)
        : entry.State is { } state ? KanbanColumn.KeyOf(state.Status) : KanbanColumn.BacklogKey;

    private static string ModelProviderTextOf(string? model, string? provider)
    {
        if (model is not null && provider is not null)
        {
            return $"{model} - {provider}";
        }

        return model ?? provider ?? ModelUnknownText;
    }

    private static string? Known(string? value, int maxChars)
    {
        var plain = PanelText.Plain(value, maxChars);
        return plain.Length == 0 ? null : plain;
    }

    private void OnCatalogPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || !string.Equals(e.PropertyName, nameof(SessionCatalogModel.Sessions), StringComparison.Ordinal))
        {
            return;
        }

        SyncEntries();
        Rebuild();
    }

    /// <summary>Reads the catalog rows into the entries: new chats appear, gone chats leave, and a row's own change replaces the entry's state.</summary>
    private void SyncEntries()
    {
        var rows = _catalog.Sessions;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            seen.Add(row.Id);
            if (!_entries.TryGetValue(row.Id, out var entry))
            {
                entry = new Entry { State = row.Card, LastRow = row.Card, LastActivity = row.Summary.LastActivity };
                _entries[row.Id] = entry;
            }
            else
            {
                AdoptRowChange(entry, row.Card);
                entry.LastActivity = Latest(entry.LastActivity, row.Summary.LastActivity);
            }

            entry.Title = row.Title;
        }

        foreach (var gone in _entries.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _entries.Remove(gone);
        }
    }

    private static void AdoptRowChange(Entry entry, KanbanCardState? row)
    {
        var previous = entry.LastRow;
        entry.LastRow = row;
        if (row is null)
        {
            if (previous is not null)
            {
                entry.State = null;
            }

            return;
        }

        if (previous is null)
        {
            entry.State = row;
            return;
        }

        var status = row.Status != previous.Status ? row.Status : entry.State?.Status ?? row.Status;
        var stale = row.IsStale != previous.IsStale ? row.IsStale : entry.State?.IsStale ?? row.IsStale;
        entry.State = new KanbanCardState(status, stale);
    }

    private void Rebuild()
    {
        foreach (var entry in _entries.Values)
        {
            entry.ExpirePlacement();
        }

        var now = _timeProvider.GetUtcNow();
        var cards = _entries
            .Select(pair => BuildCard(pair.Key, pair.Value, now))
            .OrderByDescending(card => card.LastActivity)
            .ThenBy(card => card.Id, StringComparer.Ordinal)
            .ToList();
        var columns = BuildColumns(cards);

        var cardsChanged = !cards.SequenceEqual(_cards);
        var columnsChanged = !SameColumns(columns, _columns);
        if (cardsChanged)
        {
            _cards = cards;
            NotifyPropertyChanged(nameof(Cards));
            NotifyPropertyChanged(nameof(IsEmpty));
        }

        if (columnsChanged)
        {
            _columns = columns;
            NotifyPropertyChanged(nameof(Columns));
        }
    }

    private static List<KanbanColumn> BuildColumns(List<KanbanCard> cards)
    {
        var byColumn = cards.GroupBy(card => card.ColumnKey, StringComparer.Ordinal).ToDictionary(group => group.Key, group => (IReadOnlyList<KanbanCard>)[.. group], StringComparer.Ordinal);
        IReadOnlyList<KanbanCard> CardsOf(string key) => byColumn.TryGetValue(key, out var list) ? list : [];

        var columns = new List<KanbanColumn>(RuntimeOrder.Length + 2)
        {
            new()
            {
                Key = KanbanColumn.BacklogKey,
                Title = KanbanColumn.TitleOf(UserSettableStatus.Backlog),
                UserStatus = UserSettableStatus.Backlog,
                Cards = CardsOf(KanbanColumn.BacklogKey),
            },
        };
        foreach (var status in RuntimeOrder)
        {
            var key = KanbanColumn.KeyOf(status);
            columns.Add(new KanbanColumn { Key = key, Title = KanbanColumn.TitleOf(status), RuntimeStatus = status, Cards = CardsOf(key) });
        }

        columns.Add(new KanbanColumn
        {
            Key = KanbanColumn.ArchivedKey,
            Title = KanbanColumn.TitleOf(UserSettableStatus.Archived),
            UserStatus = UserSettableStatus.Archived,
            Cards = CardsOf(KanbanColumn.ArchivedKey),
        });
        return columns;
    }

    private static bool SameColumns(List<KanbanColumn> next, IReadOnlyList<KanbanColumn> current)
    {
        if (next.Count != current.Count)
        {
            return false;
        }

        for (var index = 0; index < next.Count; index++)
        {
            if (!string.Equals(next[index].Key, current[index].Key, StringComparison.Ordinal) || !next[index].Cards.SequenceEqual(current[index].Cards))
            {
                return false;
            }
        }

        return true;
    }

    private KanbanCard BuildCard(string id, Entry entry, DateTimeOffset now)
    {
        var info = _options.ModelOf?.Invoke(id);
        var model = Known(info?.Model, MaxModelChars);
        var provider = Known(info?.Provider, MaxModelChars);
        var title = PanelText.Plain(entry.Title, MaxTitleChars);
        var state = entry.State;
        var userStatus = entry.ActivePlacement?.Status;
        var statusText = state is { } known ? KanbanColumn.TitleOf(known.Status) : NoStatusText;
        var lastActivity = RelativeTimeText.Format(entry.LastActivity, now);
        var modelProvider = ModelProviderTextOf(model, provider);
        var stale = state?.IsStale == true;
        var error = state?.Status == RuntimeStatus.Unreachable;
        var name = $"{title}, {statusText}, {modelProvider}, {lastActivity}";
        if (stale)
        {
            name += ", " + StaleText;
        }

        if (error)
        {
            name += ", " + UnreachableText;
        }

        return new KanbanCard
        {
            Id = id,
            Title = title,
            Model = model,
            Provider = provider,
            ModelProviderText = modelProvider,
            LastActivity = entry.LastActivity,
            LastActivityText = lastActivity,
            State = state,
            UserStatus = userStatus,
            ColumnKey = ColumnKeyOf(entry),
            StatusText = statusText,
            StaleLabel = stale ? StaleText : null,
            ErrorText = error ? UnreachableText : null,
            AccessibleName = name,
        };
    }

    /// <summary>A placement the user made, and the runtime status the card had when it was made.</summary>
    private sealed record Placement(UserSettableStatus Status, RuntimeStatus? PlacedAt);

    /// <summary>The mutable state of one chat; the board replaces the immutable card built from it.</summary>
    private sealed class Entry
    {
        internal string Title { get; set; } = string.Empty;

        internal DateTimeOffset LastActivity { get; set; }

        internal KanbanCardState? State { get; set; }

        internal KanbanCardState? LastRow { get; set; }

        internal Placement? Placement { get; set; }

        /// <summary>Gets the placement that still holds. <see cref="ExpirePlacement"/> ends it once the runtime status moved on.</summary>
        internal Placement? ActivePlacement => Placement;

        /// <summary>Drops the placement when the runtime status differs from the one at placement time.</summary>
        internal void ExpirePlacement()
        {
            if (Placement is { } placement && placement.PlacedAt != State?.Status)
            {
                Placement = null;
            }
        }
    }
}

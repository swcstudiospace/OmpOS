using System.ComponentModel;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Kanban;

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
/// <para>
/// State: <see cref="State"/> is Loading while the head says so (<see cref="KanbanBoardOptions.StartLoading"/>, <see cref="RetryCommand"/>), Error when the catalog
/// could not be read and the board has nothing to show, Empty when there is no session, and Ready otherwise. A session that is closed or ended
/// is never dropped by the board (DR-5b): it stays as a card in a terminal column (Completed, Failed or Archived), and a dropped engine shows as
/// Disconnected, which is distinct from Failed. A card leaves the board only when the catalog itself forgets the session.
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
    private const string DisconnectedText = "Disconnected, the engine dropped";

    private static readonly RuntimeStatus[] RuntimeOrder = Enum.GetValues<RuntimeStatus>();

    private readonly SessionCatalogModel _catalog;
    private readonly TimeProvider _timeProvider;
    private readonly KanbanBoardOptions _options;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, KanbanCardViewModel> _models = new(StringComparer.Ordinal);
    private readonly AgentPanelCommand _retryCommand;
    private readonly AgentPanelCommand _startSessionCommand;
    private List<KanbanCardViewModel> _cardModels = [];
    private BoardState _state = BoardState.Empty;
    private bool _loading;
    private string _announcement = string.Empty;
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
        _loading = _options.StartLoading;
        _retryCommand = new AgentPanelCommand(
            _ =>
            {
                Retry();
                return Task.CompletedTask;
            },
            static _ => { });
        _startSessionCommand = new AgentPanelCommand(
            _ =>
            {
                StartSessionRequested?.Invoke(this, EventArgs.Empty);
                return Task.CompletedTask;
            },
            static _ => { });
        SyncEntries();
        Rebuild();
        _catalog.PropertyChanged += OnCatalogPropertyChanged;
    }

    /// <summary>Occurs when a card is activated: the owner of the sessions opens or focuses the session named by the argument.</summary>
    public event EventHandler<SessionSelectionChangedEventArgs>? OpenSessionRequested;

    /// <summary>Occurs when the user asks, from the empty board, to start a session. The owner of the sessions starts one.</summary>
    public event EventHandler? StartSessionRequested;

    /// <summary>Gets what the tab shows: loading skeleton, empty, error with retry, or the columns.</summary>
    public BoardState State => _state;

    /// <summary>
    /// Gets the reason the sessions could not be read, or null when they could. In <see cref="BoardState.Ready"/> a text here means the cards
    /// are the last good snapshot.
    /// </summary>
    public string? ErrorText => _catalog.LoadError;

    /// <summary>Gets the command that reads the sessions again. It shows <see cref="BoardState.Loading"/> while it runs.</summary>
    public AgentPanelCommand RetryCommand => _retryCommand;

    /// <summary>Gets the command that raises <see cref="StartSessionRequested"/>.</summary>
    public AgentPanelCommand StartSessionCommand => _startSessionCommand;

    /// <summary>Gets the view models of the cards, in the order of <see cref="Cards"/>. A view model lives as long as its session is on the board.</summary>
    public IReadOnlyList<KanbanCardViewModel> CardModels => _cardModels;

    /// <summary>
    /// Gets the last text for assistive technology: a card whose status changed, or the result of a steering attempt. The view shows it in a
    /// polite live region.
    /// </summary>
    public string Announcement => _announcement;

    /// <summary>Gets the columns in board order: Backlog, one per runtime status, Archived. Every column is present, empty or not.</summary>
    public IReadOnlyList<KanbanColumn> Columns => _columns;

    /// <summary>Gets every card, one per chat, most recent activity first.</summary>
    public IReadOnlyList<KanbanCard> Cards => _cards;

    /// <summary>Gets whether the board has no chat at all.</summary>
    public bool IsEmpty => _cards.Count == 0;

    /// <summary>Returns the view model of a card.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The view model, or null when no chat has that identifier.</returns>
    public KanbanCardViewModel? CardModelOf(string sessionId) => _models.GetValueOrDefault(sessionId);

    /// <summary>Raises <see cref="OpenSessionRequested"/> for a card.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>True when the chat is on the board and the event was raised; false otherwise.</returns>
    public bool RequestOpen(string sessionId)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        if (!_entries.ContainsKey(sessionId))
        {
            return false;
        }

        OpenSessionRequested?.Invoke(this, new SessionSelectionChangedEventArgs(sessionId));
        return true;
    }

    /// <summary>Ends <see cref="BoardState.Loading"/>: the head has restored its sessions.</summary>
    public void CompleteLoading() => SetLoading(false);

    /// <summary>
    /// Reads the Substrate fields of every card from <see cref="KanbanBoardOptions.Details"/>. Without a source, or when Substrate reports
    /// nothing, the fields read unavailable; nothing is invented.
    /// </summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when every card has its fields.</returns>
    public async Task RefreshDetailsAsync(CancellationToken cancellationToken)
    {
        var models = _cardModels.ToList();
        await Task.WhenAll(models.Select(model => model.LoadDetailsAsync(_options.Details, cancellationToken))).ConfigureAwait(true);
    }

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
        foreach (var model in _models.Values)
        {
            model.Dispose();
        }
    }

    private static DateTimeOffset Latest(DateTimeOffset left, DateTimeOffset right) => left >= right ? left : right;

    private static string ColumnKeyOf(Entry entry)
    {
        if (entry.ActivePlacement is { } placement)
        {
            return KanbanColumn.KeyOf(placement.Status);
        }

        return entry.State switch
        {
            { Status: RuntimeStatus.Paused } when entry.IsClosed => KanbanColumn.ArchivedKey,
            { } state => KanbanColumn.KeyOf(state.Status),
            _ => KanbanColumn.BacklogKey,
        };
    }

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
        if (_disposed)
        {
            return;
        }

        if (string.Equals(e.PropertyName, nameof(SessionCatalogModel.LoadError), StringComparison.Ordinal))
        {
            NotifyPropertyChanged(nameof(ErrorText));
            UpdateState();
            return;
        }

        if (!string.Equals(e.PropertyName, nameof(SessionCatalogModel.Sessions), StringComparison.Ordinal))
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
            entry.Kind = row.Summary.Kind;
            entry.IsClosed = row.Status == AgentRowStatus.Closed;
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
        var announcements = AnnouncementsOf(cards, columns);

        var cardsChanged = !cards.SequenceEqual(_cards);
        var columnsChanged = !SameColumns(columns, _columns);
        if (cardsChanged)
        {
            _cards = cards;
        }

        if (columnsChanged)
        {
            _columns = columns;
        }

        SyncModels(cards);
        if (cardsChanged)
        {
            NotifyPropertyChanged(nameof(Cards));
            NotifyPropertyChanged(nameof(IsEmpty));
        }

        if (columnsChanged)
        {
            NotifyPropertyChanged(nameof(Columns));
        }

        UpdateState();
        if (announcements.Count > 0)
        {
            Announce(string.Join(". ", announcements));
        }
    }

    /// <summary>Keeps one view model per card: new cards get one, changed cards hand it the new snapshot, gone cards dispose theirs.</summary>
    private void SyncModels(List<KanbanCard> cards)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var models = new List<KanbanCardViewModel>(cards.Count);
        foreach (var card in cards)
        {
            ids.Add(card.Id);
            if (_models.TryGetValue(card.Id, out var model))
            {
                model.Update(card);
            }
            else
            {
                model = new KanbanCardViewModel(card, _options.Steering, Announce, id => RequestOpen(id));
                _models[card.Id] = model;
            }

            models.Add(model);
        }

        foreach (var gone in _models.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            _models[gone].Dispose();
            _models.Remove(gone);
        }

        if (!models.SequenceEqual(_cardModels))
        {
            _cardModels = models;
            NotifyPropertyChanged(nameof(CardModels));
        }
    }

    /// <summary>Words for each card whose status, disconnected state or column changed since the last build. A new card says nothing.</summary>
    private List<string> AnnouncementsOf(List<KanbanCard> cards, List<KanbanColumn> columns)
    {
        var words = new List<string>();
        foreach (var card in cards)
        {
            var before = _cards.FirstOrDefault(previous => string.Equals(previous.Id, card.Id, StringComparison.Ordinal));
            if (before is null)
            {
                continue;
            }

            if (!string.Equals(before.StatusText, card.StatusText, StringComparison.Ordinal) || before.IsDisconnected != card.IsDisconnected)
            {
                words.Add($"{card.Title}: {card.DisconnectedText ?? card.StatusText}");
            }
            else if (!string.Equals(before.ColumnKey, card.ColumnKey, StringComparison.Ordinal))
            {
                var title = columns.First(column => string.Equals(column.Key, card.ColumnKey, StringComparison.Ordinal)).Title;
                words.Add($"{card.Title}: moved to {title}");
            }
        }

        return words;
    }

    private void Announce(string text)
    {
        _announcement = text;
        NotifyPropertyChanged(nameof(Announcement));
    }

    private void Retry()
    {
        SetLoading(true);
        try
        {
            _ = _catalog.Refresh();
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool value)
    {
        if (_loading == value)
        {
            return;
        }

        _loading = value;
        UpdateState();
    }

    private void UpdateState()
    {
        var next = _loading
            ? BoardState.Loading
            : _cards.Count > 0 ? BoardState.Ready : _catalog.LoadError is not null ? BoardState.Error : BoardState.Empty;
        if (_state != next)
        {
            _state = next;
            NotifyPropertyChanged(nameof(State));
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
        var kindText = KindTextOf(entry.Kind);
        if (stale)
        {
            name += ", " + StaleText;
        }

        if (error)
        {
            name += ", " + UnreachableText;
        }

        var disconnected = error ? DisconnectedText : null;
        var spoken = $"{name}, {kindText}";
        if (disconnected is not null)
        {
            spoken += ", " + disconnected;
        }

        if (entry.IsClosed)
        {
            spoken += ", closed";
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
            Kind = entry.Kind,
            KindText = kindText,
            IsClosed = entry.IsClosed,
            DisconnectedText = disconnected,
            SpokenName = spoken,
        };
    }

    private static string KindTextOf(SessionKind kind) => kind switch
    {
        SessionKind.Terminal => "Terminal",
        _ => "Chat",
    };

    /// <summary>A placement the user made, and the runtime status the card had when it was made.</summary>
    private sealed record Placement(UserSettableStatus Status, RuntimeStatus? PlacedAt);

    /// <summary>The mutable state of one chat; the board replaces the immutable card built from it.</summary>
    private sealed class Entry
    {
        internal string Title { get; set; } = string.Empty;

        internal DateTimeOffset LastActivity { get; set; }

        internal SessionKind Kind { get; set; }

        internal bool IsClosed { get; set; }

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

using System.Globalization;
using OmpOS.Contracts.Agents;
using OmpOS.Presentation.Kanban;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.AgentPanel;

/// <summary>
/// The Agents rail: the sessions of an <see cref="ISessionCatalog"/> as grouped, sorted, filterable rows with a selection. The catalog (the
/// registry behind it) owns the sessions; this model never creates, changes or removes one, it only reads snapshots and re-derives rows
/// when the catalog raises <see cref="ISessionCatalog.Changed"/> or the clock moves.
/// </summary>
/// <remarks>
/// <para>
/// Status: <see cref="AgentRowStatusMap"/> maps lifecycle, live runtime status and closed flag to the row status. A Running row is stale
/// when its last activity is older than <see cref="SessionCatalogOptions.StaleAfter"/> and long-running when its session was created
/// more than <see cref="SessionCatalogOptions.LongRunningAfter"/> ago; both are flags, the status stays Running.
/// </para>
/// <para>
/// Order: groups by <see cref="AgentRowGroup"/>; inside <see cref="AgentRowGroup.NeedsYou"/> the oldest activity first (it waited longest),
/// inside every other group the most recent activity first; identifiers break ties.
/// </para>
/// <para>
/// Threading: the catalog may raise <see cref="ISessionCatalog.Changed"/> on any thread, so the handler goes through
/// <see cref="SessionCatalogOptions.Dispatch"/>; everything else runs on the context that owns the models.
/// </para>
/// </remarks>
public sealed class SessionCatalogModel : ObservableModel, IDisposable
{
    /// <summary>The most characters kept of the filter text.</summary>
    public const int MaxFilterChars = 100;

    private const int MaxTitleChars = 200;
    private const int MaxTagChars = 40;
    private const int GraphTagChars = 8;

    private readonly ISessionCatalog _catalog;
    private readonly TimeProvider _timeProvider;
    private readonly SessionCatalogOptions _options;
    private readonly Action<Action> _dispatch;
    private readonly AgentPanelCommand _selectCommand;
    private readonly AgentPanelCommand _closeSelectedCommand;
    private IReadOnlyList<SessionSummary> _snapshot = [];
    private List<AgentRow> _sessions = [];
    private List<AgentRow> _rows = [];
    private string _filter = string.Empty;
    private string? _selectedId;
    private string? _loadError;
    private ITimer? _timer;
    private bool _disposed;

    /// <summary>Creates the model, reads the first snapshot and subscribes to the catalog.</summary>
    /// <param name="catalog">The catalog that owns the sessions.</param>
    /// <param name="timeProvider">The clock of the relative times and thresholds.</param>
    /// <param name="options">The thresholds and head hooks. Null uses the defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="timeProvider"/> is null.</exception>
    public SessionCatalogModel(ISessionCatalog catalog, TimeProvider timeProvider, SessionCatalogOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _catalog = catalog;
        _timeProvider = timeProvider;
        _options = options ?? new SessionCatalogOptions();
        _dispatch = _options.Dispatch ?? (static action => action());
        _selectCommand = new AgentPanelCommand(
            parameter =>
            {
                Select(parameter as string ?? (parameter as AgentRow)?.Id);
                return Task.CompletedTask;
            },
            static _ => { });
        _closeSelectedCommand = new AgentPanelCommand(
            _ =>
            {
                if (_selectedId is { } id)
                {
                    CloseRequested?.Invoke(this, new SessionSelectionChangedEventArgs(id));
                }

                return Task.CompletedTask;
            },
            static _ => { },
            _ => _selectedId is not null);
        _catalog.Changed += OnCatalogChanged;
        Refresh();
    }

    /// <summary>Occurs after the selection changed, including when the selected session disappeared from the catalog and the selection cleared.</summary>
    public event EventHandler<SessionSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>Occurs when the user asks to close the selected session through <see cref="CloseSelectedCommand"/>. The owner of the engines closes it.</summary>
    public event EventHandler<SessionSelectionChangedEventArgs>? CloseRequested;

    /// <summary>Gets every session as a row, grouped and sorted, ignoring the filter. Replaced as a whole when anything changes.</summary>
    public IReadOnlyList<AgentRow> Sessions => _sessions;

    /// <summary>Gets the rows that match <see cref="Filter"/>, grouped and sorted. Replaced as a whole when anything changes.</summary>
    public IReadOnlyList<AgentRow> Rows => _rows;

    /// <summary>Gets the number of sessions, ignoring the filter.</summary>
    public int TotalCount => _sessions.Count;

    /// <summary>Gets or sets the filter text. Rows match when the filter occurs, ignoring case, in the title, task, graph or status text.</summary>
    public string Filter
    {
        get => _filter;
        set
        {
            var next = PanelText.Plain(value, MaxFilterChars);
            if (string.Equals(_filter, next, StringComparison.Ordinal))
            {
                return;
            }

            _filter = next;
            NotifyPropertyChanged();
            ApplyFilter();
        }
    }

    /// <summary>Gets whether a filter is set and hides every session.</summary>
    public bool HasNoMatches => _filter.Length > 0 && _rows.Count == 0 && _sessions.Count > 0;

    /// <summary>Gets the heading of the filter-matches-nothing state.</summary>
    public string NoMatchHeading => AgentPanelCopy.FilterNoMatchHeading(_filter);

    /// <summary>Gets the body of the filter-matches-nothing state.</summary>
    public string NoMatchBody => AgentPanelCopy.FilterNoMatchBody(_sessions.Count);

    /// <summary>Gets the identifier of the selected session, or null when none is selected.</summary>
    public string? SelectedId => _selectedId;

    /// <summary>Gets the selected session's row, or null. A selected session that the filter hides is still found here.</summary>
    public AgentRow? Selected => _selectedId is null ? null : _sessions.FirstOrDefault(row => string.Equals(row.Id, _selectedId, StringComparison.Ordinal));

    /// <summary>Gets the reason the last <see cref="Refresh"/> failed, or null when it succeeded. The rows then still show the last good snapshot.</summary>
    public string? LoadError => _loadError;

    /// <summary>Gets the command that selects a session. Its parameter is a session identifier or an <see cref="AgentRow"/>.</summary>
    public AgentPanelCommand SelectCommand => _selectCommand;

    /// <summary>Gets the command that asks the owner of the engines to close the selected session. It is disabled while nothing is selected.</summary>
    public AgentPanelCommand CloseSelectedCommand => _closeSelectedCommand;

    /// <summary>Reads a fresh snapshot from the catalog and re-derives every row.</summary>
    /// <returns>True when the snapshot was read. False when the catalog threw: <see cref="LoadError"/> then says why and the rows stay as they were.</returns>
    public bool Refresh()
    {
        IReadOnlyList<SessionSummary> snapshot;
        try
        {
            snapshot = _catalog.Snapshot();
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            var reason = PanelText.Plain(fault.Message, 200);
            SetProperty(ref _loadError, reason.Length == 0 ? "The session list could not be read" : reason, nameof(LoadError));
            return false;
        }

        SetProperty(ref _loadError, null, nameof(LoadError));
        _snapshot = snapshot;
        Rebuild();
        return true;
    }

    /// <summary>Re-derives the relative times, stale and long-running flags from the clock, without reading the catalog again.</summary>
    public void RefreshTimes() => Rebuild();

    /// <summary>Starts refreshing the relative times every <see cref="SessionCatalogOptions.RefreshInterval"/>. The refresh is silent: no announcement.</summary>
    public void StartRefreshing()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer ??= _timeProvider.CreateTimer(
            _ => _dispatch(RefreshTimesIfLive),
            null,
            _options.RefreshInterval,
            _options.RefreshInterval);
    }

    /// <summary>Stops the periodic refresh.</summary>
    public void StopRefreshing()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>Selects a session, or clears the selection.</summary>
    /// <param name="sessionId">The session to select, or null to clear the selection.</param>
    /// <returns>True when the selection now equals <paramref name="sessionId"/>. False when the catalog has no such session; the selection is then unchanged.</returns>
    public bool Select(string? sessionId)
    {
        if (sessionId is not null && !_sessions.Any(row => string.Equals(row.Id, sessionId, StringComparison.Ordinal)))
        {
            return false;
        }

        SetSelection(sessionId);
        return true;
    }

    /// <summary>Stops the refresh and unsubscribes from the catalog.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopRefreshing();
        _catalog.Changed -= OnCatalogChanged;
    }

    private void OnCatalogChanged(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            _dispatch(RefreshIfLive);
        }
    }

    private void RefreshIfLive()
    {
        if (!_disposed)
        {
            Refresh();
        }
    }

    private void RefreshTimesIfLive()
    {
        if (!_disposed)
        {
            RefreshTimes();
        }
    }

    private void SetSelection(string? sessionId)
    {
        if (string.Equals(_selectedId, sessionId, StringComparison.Ordinal))
        {
            return;
        }

        _selectedId = sessionId;
        NotifyPropertyChanged(nameof(SelectedId));
        NotifyPropertyChanged(nameof(Selected));
        _closeSelectedCommand.NotifyCanExecuteChanged();
        SelectionChanged?.Invoke(this, new SessionSelectionChangedEventArgs(sessionId));
    }

    private void Rebuild()
    {
        var now = _timeProvider.GetUtcNow();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<AgentRow>(_snapshot.Count);
        foreach (var summary in _snapshot)
        {
            if (seen.Add(summary.Id))
            {
                rows.Add(BuildRow(summary, now));
            }
        }

        rows.Sort(CompareRows);
        var sessions = MarkGroupStarts(rows);
        var changed = !sessions.SequenceEqual(_sessions);
        _sessions = sessions;
        if (changed)
        {
            NotifyPropertyChanged(nameof(Sessions));
            NotifyPropertyChanged(nameof(TotalCount));
            NotifyPropertyChanged(nameof(Selected));
        }

        ApplyFilter();
        if (_selectedId is not null && !seen.Contains(_selectedId))
        {
            SetSelection(null);
        }
    }

    private void ApplyFilter()
    {
        var matching = _filter.Length == 0 ? _sessions : MarkGroupStarts(_sessions.Where(Matches).ToList());
        if (matching.SequenceEqual(_rows))
        {
            return;
        }

        _rows = matching;
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(HasNoMatches));
        NotifyPropertyChanged(nameof(NoMatchHeading));
        NotifyPropertyChanged(nameof(NoMatchBody));
    }

    private bool Matches(AgentRow row) =>
        Contains(row.Title) || Contains(row.TaskId) || Contains(row.GraphId) || Contains(row.StatusText) || Contains(row.TaskTag);

    private bool Contains(string? text) => text is not null && text.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    private static int CompareRows(AgentRow left, AgentRow right)
    {
        var byGroup = left.Group.CompareTo(right.Group);
        if (byGroup != 0)
        {
            return byGroup;
        }

        var byTime = left.Group == AgentRowGroup.NeedsYou
            ? left.Summary.LastActivity.CompareTo(right.Summary.LastActivity)
            : right.Summary.LastActivity.CompareTo(left.Summary.LastActivity);
        return byTime != 0 ? byTime : string.CompareOrdinal(left.Id, right.Id);
    }

    private static List<AgentRow> MarkGroupStarts(List<AgentRow> rows)
    {
        var marked = new List<AgentRow>(rows.Count);
        AgentRowGroup? previous = null;
        foreach (var row in rows)
        {
            marked.Add(row with { IsGroupStart = previous != row.Group });
            previous = row.Group;
        }

        return marked;
    }

    private AgentRow BuildRow(SessionSummary summary, DateTimeOffset now)
    {
        var mapping = AgentRowStatusMap.Resolve(summary.Lifecycle, _options.LiveStatus?.Invoke(summary.Id), _options.IsClosed?.Invoke(summary.Id) ?? false);
        var running = mapping.Status == AgentRowStatus.Running;
        var idle = now - summary.LastActivity;
        var isStale = running && idle > _options.StaleAfter;
        var isLongRunning = running && now - summary.CreatedAt > _options.LongRunningAfter;
        var title = PanelText.Plain(summary.Title, MaxTitleChars);
        if (title.Length == 0)
        {
            title = AgentPanelCopy.UntitledAgent;
        }

        var statusText = AgentRowStatusMap.TextOf(mapping.Status);
        var lastActivity = RelativeTimeText.Format(summary.LastActivity, now);
        var quietMinutes = isStale ? Math.Max(1, (long)idle.TotalMinutes) : (long?)null;
        var quiet = quietMinutes is { } minutes ? string.Create(CultureInfo.InvariantCulture, $"Quiet for {minutes} min") : null;
        var taskId = PanelText.Plain(summary.TaskId, MaxTagChars);
        var graphId = PanelText.Plain(summary.GraphId, MaxTagChars);
        var tag = taskId.Length > 0 ? $"Task {taskId}" : graphId.Length > 0 ? $"Graph {LastChars(graphId, GraphTagChars)}" : null;
        return new AgentRow
        {
            Summary = summary,
            Id = summary.Id,
            Title = title,
            Status = mapping.Status,
            Group = mapping.Group,
            Card = mapping.RuntimeStatus is { } runtime ? new KanbanCardState(runtime, isStale) : null,
            StatusText = statusText,
            IconKey = AgentRowStatusMap.IconOf(mapping.Status),
            LastActivityText = lastActivity,
            IsLongRunning = isLongRunning,
            IsStale = isStale,
            IsLost = mapping.Status == AgentRowStatus.Lost,
            QuietText = quiet,
            TaskTag = tag,
            TaskId = taskId.Length == 0 ? null : taskId,
            GraphId = graphId.Length == 0 ? null : graphId,
            AccessibleName = AccessibleNameOf(title, statusText, lastActivity, isLongRunning, quietMinutes, tag, summary.Kind == SessionKind.Terminal),
        };
    }

    private static string LastChars(string value, int count) => value.Length <= count ? value : value[^count..];

    private static string AccessibleNameOf(string title, string statusText, string lastActivity, bool longRunning, long? quietMinutes, string? tag, bool terminal)
    {
        var name = $"{title}, {statusText}, {lastActivity}";
        if (longRunning)
        {
            name += ", long-running";
        }

        if (quietMinutes is { } minutes)
        {
            name += string.Create(CultureInfo.InvariantCulture, $", quiet for {minutes} min");
        }

        if (terminal)
        {
            name += $", {AgentPanelCopy.TerminalAccessibleTag}";
        }

        return tag is null ? name : $"{name}, {tag}";
    }
}

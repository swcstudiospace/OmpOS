using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// The Cowork rail: rows grouped by project, a filter, one selection, and the full-list sentence.
/// A catalog change replaces rows the way a board replaces cards, without sharing that board.
/// </summary>
public sealed class CoworkSessionListModel : ObservableModel, IDisposable
{
    /// <summary>The most Cowork tasks the list reports as full.</summary>
    public const int SessionCap = 200;

    /// <summary>The most characters kept of the filter text.</summary>
    public const int MaxFilterChars = 100;

    private readonly ICoworkSessionCatalog _catalog;
    private readonly EventHandler _onChanged;
    private List<CoworkSessionRow> _sessions = [];
    private List<CoworkSessionGroup> _groups = [];
    private string _filter = string.Empty;
    private string? _selectedId;
    private bool _disposed;

    /// <summary>Creates the list and reads the first snapshot.</summary>
    /// <param name="catalog">The Cowork catalog. It is not a Code catalog.</param>
    public CoworkSessionListModel(ICoworkSessionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
        _onChanged = (_, _) => SyncEntries();
        catalog.Changed += _onChanged;
        SyncEntries();
    }

    /// <summary>Occurs after the rows, the filter or the selection change.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets every row, ignoring the filter, in catalog order.</summary>
    public IReadOnlyList<CoworkSessionRow> Sessions => _sessions;

    /// <summary>Gets the filtered rows grouped by project. A project with no match is absent.</summary>
    public IReadOnlyList<CoworkSessionGroup> Groups => _groups;

    /// <summary>Gets the filtered rows in catalog order.</summary>
    public IReadOnlyList<CoworkSessionRow> Rows => _groups.SelectMany(group => group.Rows).ToArray();

    /// <summary>Gets the number of tasks, ignoring the filter.</summary>
    public int TotalCount => _sessions.Count;

    /// <summary>Gets the full-list sentence when the catalog holds <see cref="SessionCap"/> tasks; otherwise null.</summary>
    public string? LimitMessage => _sessions.Count >= SessionCap ? CoworkCopy.SessionLimit : null;

    /// <summary>Gets or sets the filter. A row matches when the text occurs, ignoring case, in the title or the project id.</summary>
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
            RebuildGroups();
            NotifyPropertyChanged();
            Raise();
        }
    }

    /// <summary>Gets the selected session identifier, or null.</summary>
    public string? SelectedId => _selectedId;

    /// <summary>Gets the selected row, or null. A selected row the filter hides is still found here.</summary>
    public CoworkSessionRow? Selected => _selectedId is null
        ? null
        : _sessions.Find(row => string.Equals(row.Id, _selectedId, StringComparison.Ordinal));

    /// <summary>Selects one row. An unknown identifier is ignored. Null clears the selection.</summary>
    /// <param name="id">The session identifier, or null.</param>
    public void Select(string? id)
    {
        if (id is not null && _sessions.TrueForAll(row => !string.Equals(row.Id, id, StringComparison.Ordinal)))
        {
            return;
        }

        if (string.Equals(_selectedId, id, StringComparison.Ordinal))
        {
            return;
        }

        _selectedId = id;
        NotifyPropertyChanged(nameof(SelectedId));
        NotifyPropertyChanged(nameof(Selected));
        Raise();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _catalog.Changed -= _onChanged;
    }

    /// <summary>Reads the catalog into the rows: new tasks appear, gone tasks leave, and a changed summary replaces the row.</summary>
    private void SyncEntries()
    {
        if (_disposed)
        {
            return;
        }

        var snapshot = _catalog.Snapshot();
        var next = new List<CoworkSessionRow>(snapshot.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var summary in snapshot)
        {
            if (!seen.Add(summary.Id))
            {
                continue;
            }

            next.Add(CoworkSessionRow.From(summary));
        }

        _sessions = next;
        if (_selectedId is not null && _sessions.TrueForAll(row => !string.Equals(row.Id, _selectedId, StringComparison.Ordinal)))
        {
            _selectedId = null;
            NotifyPropertyChanged(nameof(SelectedId));
            NotifyPropertyChanged(nameof(Selected));
        }

        RebuildGroups();
        NotifyPropertyChanged(nameof(Sessions));
        NotifyPropertyChanged(nameof(TotalCount));
        NotifyPropertyChanged(nameof(LimitMessage));
        Raise();
    }

    private void RebuildGroups()
    {
        var matched = _filter.Length == 0
            ? _sessions
            : _sessions.Where(Matches).ToList();
        var order = new List<string?>();
        var byProject = new Dictionary<string, List<CoworkSessionRow>>(StringComparer.Ordinal);
        List<CoworkSessionRow>? ungrouped = null;
        foreach (var row in matched)
        {
            if (row.ProjectId is null)
            {
                ungrouped ??= [];
                ungrouped.Add(row);
                continue;
            }

            if (!byProject.TryGetValue(row.ProjectId, out var list))
            {
                list = [];
                byProject[row.ProjectId] = list;
                order.Add(row.ProjectId);
            }

            list.Add(row);
        }

        var groups = new List<CoworkSessionGroup>(order.Count + 1);
        foreach (var projectId in order)
        {
            groups.Add(new CoworkSessionGroup(projectId, projectId!, byProject[projectId!]));
        }

        if (ungrouped is not null)
        {
            groups.Add(new CoworkSessionGroup(null, CoworkCopy.NoProject, ungrouped));
        }

        _groups = groups;
        NotifyPropertyChanged(nameof(Groups));
        NotifyPropertyChanged(nameof(Rows));
    }

    private bool Matches(CoworkSessionRow row) =>
        row.Title.Contains(_filter, StringComparison.OrdinalIgnoreCase)
        || (row.ProjectId?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false);

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

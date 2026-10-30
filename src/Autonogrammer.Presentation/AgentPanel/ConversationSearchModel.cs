using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// The state of the conversation search overlay (Cmd/Ctrl+K in the Agents Window): a query box over a list of the conversations whose title or transcript match, each with
/// a snippet. The model searches a <see cref="ConversationSearchIndex"/>, which is bounded and in memory, so the results follow every keystroke with no wait. Up and Down move
/// the selection (wrapping), <see cref="Accept"/> yields the session to open, <see cref="Close"/> hides the overlay and clears the query.
/// </summary>
public sealed class ConversationSearchModel : ObservableModel
{
    /// <summary>The most results the overlay lists.</summary>
    public const int MaxResults = 30;

    private readonly ConversationSearchIndex _index;
    private bool _isOpen;
    private string _query = string.Empty;
    private IReadOnlyList<ConversationSearchResult> _results = [];
    private int _selectedIndex = -1;

    /// <summary>Creates a closed search over an index.</summary>
    /// <param name="index">The index the host keeps up to date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="index"/> is null.</exception>
    public ConversationSearchModel(ConversationSearchIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);

        _index = index;
    }

    /// <summary>Gets the index the search reads.</summary>
    public ConversationSearchIndex Index => _index;

    /// <summary>Gets whether the overlay is showing.</summary>
    public bool IsOpen { get => _isOpen; private set => SetProperty(ref _isOpen, value); }

    /// <summary>Gets the query typed so far.</summary>
    public string Query => _query;

    /// <summary>Gets the matching conversations, best first.</summary>
    public IReadOnlyList<ConversationSearchResult> Results
    {
        get => _results;
        private set
        {
            if (SetProperty(ref _results, value))
            {
                NotifyPropertyChanged(nameof(SelectedResult));
                NotifyPropertyChanged(nameof(Announcement));
            }
        }
    }

    /// <summary>Gets the index of the selected result, or -1.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        private set
        {
            if (SetProperty(ref _selectedIndex, value))
            {
                NotifyPropertyChanged(nameof(SelectedResult));
            }
        }
    }

    /// <summary>Gets the selected result, or <see langword="null"/>.</summary>
    public ConversationSearchResult? SelectedResult => _selectedIndex >= 0 && _selectedIndex < _results.Count ? _results[_selectedIndex] : null;

    /// <summary>Gets the text shown instead of a list: the prompt before anything is typed, or the no-match sentence. It is <see langword="null"/> while results are listed.</summary>
    public string? EmptyMessage
    {
        get
        {
            if (_results.Count > 0)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(_query) ? ConversationSearchCopy.Prompt : ConversationSearchCopy.NoMatches(_query);
        }
    }

    /// <summary>Gets the sentence for the live region: how many conversations match, or <see langword="null"/> before a query is typed.</summary>
    public string? Announcement => string.IsNullOrWhiteSpace(_query) ? null : ConversationSearchCopy.ResultCount(_results.Count);

    /// <summary>Opens the overlay with an empty query.</summary>
    public void Open()
    {
        Reset();
        IsOpen = true;
    }

    /// <summary>Hides the overlay and clears the query and the results.</summary>
    public void Close()
    {
        Reset();
        IsOpen = false;
    }

    /// <summary>Sets the query and searches. The first result is selected.</summary>
    /// <param name="query">The query.</param>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public void SetQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        _query = query;
        NotifyPropertyChanged(nameof(Query));
        Results = _index.Search(query, MaxResults);
        SelectedIndex = _results.Count > 0 ? 0 : -1;
        NotifyPropertyChanged(nameof(EmptyMessage));
    }

    /// <summary>Moves the selection by <paramref name="delta"/> results, wrapping past either end. It does nothing when nothing is listed.</summary>
    /// <param name="delta">The number of results to move; negative moves up.</param>
    public void MoveSelection(int delta)
    {
        if (_results.Count == 0)
        {
            return;
        }

        var count = _results.Count;
        SelectedIndex = (((_selectedIndex + delta) % count) + count) % count;
    }

    /// <summary>Selects a result by index (a click).</summary>
    /// <param name="index">The index in <see cref="Results"/>.</param>
    /// <returns><see langword="true"/> when the index named a result.</returns>
    public bool Select(int index)
    {
        if (index < 0 || index >= _results.Count)
        {
            return false;
        }

        SelectedIndex = index;
        return true;
    }

    /// <summary>Takes the selected result and closes the overlay.</summary>
    /// <returns>The identifier of the session to open, or <see langword="null"/> when nothing is selected.</returns>
    public string? Accept()
    {
        if (SelectedResult is not { } result)
        {
            return null;
        }

        Close();
        return result.SessionId;
    }

    /// <summary>Brings the index in line with the catalog rows of the Agents rail: updates every title, adds sessions that are new, and removes sessions that are gone. Transcript text is kept.</summary>
    /// <param name="rows">The rows of the rail.</param>
    /// <exception cref="ArgumentNullException"><paramref name="rows"/> is null.</exception>
    public void SyncSessions(IReadOnlyList<AgentRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        foreach (var row in rows)
        {
            _index.Upsert(row.Id, row.Title, null);
        }

        _index.RetainOnly(rows.Select(static row => row.Id).ToHashSet(StringComparer.Ordinal));
        if (_isOpen)
        {
            SetQuery(_query);
        }
    }

    private void Reset()
    {
        _query = string.Empty;
        NotifyPropertyChanged(nameof(Query));
        Results = [];
        SelectedIndex = -1;
        NotifyPropertyChanged(nameof(EmptyMessage));
    }
}

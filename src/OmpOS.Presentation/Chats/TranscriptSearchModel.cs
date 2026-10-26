using System.ComponentModel;
using OmpOS.Presentation.Observable;
using OmpOS.Presentation.State;

namespace OmpOS.Presentation.Chats;

/// <summary>
/// The search state over one session's transcript: the query, its matches, and the selected match. It follows the session's
/// <see cref="SessionViewModel.Tree"/>, so matches stay current while the transcript streams.
/// </summary>
/// <remarks>
/// Selection wraps: <see cref="Next"/> past the last match selects the first, and <see cref="Previous"/> before the first
/// selects the last. The selection is always valid: <see cref="SelectedIndex"/> is -1 exactly when there are no matches.
/// Every explicit selection (typing a query, <see cref="Next"/>, <see cref="Previous"/>, <see cref="Select"/>) raises
/// <see cref="MatchSelected"/>, even when the selected index does not change, so a view can scroll to the match again.
/// </remarks>
public sealed class TranscriptSearchModel : ObservableModel, IDisposable
{
    private readonly SessionViewModel _session;
    private string _query = string.Empty;
    private IReadOnlyList<TranscriptMatch> _matches = [];
    private int _selectedIndex = -1;

    /// <summary>Creates the search over one session. The model unsubscribes when disposed.</summary>
    /// <param name="session">The session whose transcript is searched.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public TranscriptSearchModel(SessionViewModel session)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _session.PropertyChanged += OnSessionPropertyChanged;
    }

    /// <summary>Raised on every explicit selection of a match, with the match that is now selected.</summary>
    public event EventHandler<MatchSelectedEventArgs>? MatchSelected;

    /// <summary>Gets the current query. Empty when no search is active.</summary>
    public string Query
    {
        get => _query;
        private set
        {
            if (SetProperty(ref _query, value))
            {
                NotifyPropertyChanged(nameof(CountLabel));
            }
        }
    }

    /// <summary>Gets the matches of the current query in the current transcript, in order.</summary>
    public IReadOnlyList<TranscriptMatch> Matches
    {
        get => _matches;
        private set
        {
            if (SetProperty(ref _matches, value))
            {
                NotifyPropertyChanged(nameof(MatchCount));
                NotifyPropertyChanged(nameof(CountLabel));
            }
        }
    }

    /// <summary>Gets the number of matches.</summary>
    public int MatchCount => _matches.Count;

    /// <summary>Gets the index of the selected match within <see cref="Matches"/>, or -1 when there are no matches.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        private set
        {
            if (SetProperty(ref _selectedIndex, value))
            {
                NotifyPropertyChanged(nameof(SelectedBlockIndex));
                NotifyPropertyChanged(nameof(CountLabel));
            }
        }
    }

    /// <summary>Gets the block index of the selected match, the one a view scrolls to; -1 when there are no matches.</summary>
    public int SelectedBlockIndex => _selectedIndex < 0 ? -1 : _matches[_selectedIndex].BlockIndex;

    /// <summary>
    /// Gets the status text for the search: empty with no query, "No matches" for none, and otherwise the selected position
    /// and the total, such as "2 of 5 matches".
    /// </summary>
    public string CountLabel
    {
        get
        {
            if (_query.Length == 0)
            {
                return string.Empty;
            }

            return _matches.Count switch
            {
                0 => "No matches",
                1 => "1 of 1 match",
                var count => $"{_selectedIndex + 1} of {count} matches",
            };
        }
    }

    /// <summary>Sets the query and searches the current transcript. The first match, if any, becomes the selection.</summary>
    /// <param name="query">The text to find. Empty clears the search.</param>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public void SetQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        Query = query;
        Matches = TranscriptSearch.Find(_session.Tree, query);
        SelectedIndex = _matches.Count > 0 ? 0 : -1;
        RaiseSelected();
    }

    /// <summary>Selects the next match, wrapping from the last to the first. Does nothing when there are no matches.</summary>
    public void Next()
    {
        if (_matches.Count == 0)
        {
            return;
        }

        SelectedIndex = _selectedIndex < 0 ? 0 : (_selectedIndex + 1) % _matches.Count;
        RaiseSelected();
    }

    /// <summary>Selects the previous match, wrapping from the first to the last. Does nothing when there are no matches.</summary>
    public void Previous()
    {
        if (_matches.Count == 0)
        {
            return;
        }

        SelectedIndex = _selectedIndex <= 0 ? _matches.Count - 1 : _selectedIndex - 1;
        RaiseSelected();
    }

    /// <summary>Selects the match at <paramref name="index"/>.</summary>
    /// <param name="index">The index within <see cref="Matches"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the matches.</exception>
    public void Select(int index)
    {
        if (index < 0 || index >= _matches.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is outside the matches.");
        }

        SelectedIndex = index;
        RaiseSelected();
    }

    /// <summary>Stops following the session's transcript.</summary>
    public void Dispose() => _session.PropertyChanged -= OnSessionPropertyChanged;

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SessionViewModel.Tree), StringComparison.Ordinal) || _query.Length == 0)
        {
            return;
        }

        // The transcript changed under an active query: re-search and keep the selection on a valid match. No event is
        // raised, so a streaming update never moves the view away from the match the user chose.
        Matches = TranscriptSearch.Find(_session.Tree, _query);
        SelectedIndex = _matches.Count == 0 ? -1 : _selectedIndex < 0 ? 0 : Math.Min(_selectedIndex, _matches.Count - 1);
    }

    private void RaiseSelected()
    {
        if (_selectedIndex >= 0)
        {
            MatchSelected?.Invoke(this, new MatchSelectedEventArgs(_matches[_selectedIndex]));
        }
    }
}

using System.ComponentModel;
using Autonogrammer.Presentation.Observable;
using Autonogrammer.Presentation.State;

namespace Autonogrammer.Presentation.Chats;

/// <summary>
/// The search state over one session's transcript and, optionally, the side chats opened from it: the query, its matches, and
/// the selected match. It follows the session's <see cref="SessionViewModel.Tree"/> and the side chats' messages, so matches
/// stay current while either streams.
/// </summary>
/// <remarks>
/// <para>
/// Selection wraps: <see cref="Next"/> past the last match selects the first, and <see cref="Previous"/> before the first
/// selects the last. The selection is always valid: <see cref="SelectedIndex"/> is -1 exactly when there are no matches.
/// Every explicit selection (typing a query, <see cref="Next"/>, <see cref="Previous"/>, <see cref="Select"/>, changing the
/// <see cref="Scope"/>) raises <see cref="MatchSelected"/>, even when the selected index does not change, so a view can
/// scroll to the match again.
/// </para>
/// <para>
/// The scope starts as <see cref="SearchScope.Transcript"/>. With <see cref="SearchScope.TranscriptAndSideChats"/> the matches
/// of the parent transcript come first, then those of each side chat in the order the chats were opened. A match in a side
/// chat carries <see cref="TranscriptMatch.ChatId"/>; its <see cref="TranscriptMatch.BlockIndex"/> is then a message index
/// of that chat, not a transcript block, so a view scrolls the transcript only when <see cref="SelectedBlockIndex"/> is not -1.
/// </para>
/// </remarks>
public sealed class TranscriptSearchModel : ObservableModel, IDisposable
{
    private readonly SessionViewModel _session;
    private readonly SideChatPanel? _sideChats;
    private readonly List<SideChat> _watchedChats = [];
    private string _query = string.Empty;
    private SearchScope _scope = SearchScope.Transcript;
    private IReadOnlyList<TranscriptMatch> _matches = [];
    private int _selectedIndex = -1;

    /// <summary>Creates the search over one session's transcript alone. The model unsubscribes when disposed.</summary>
    /// <param name="session">The session whose transcript is searched.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public TranscriptSearchModel(SessionViewModel session)
        : this(session, null)
    {
    }

    /// <summary>
    /// Creates the search over one session's transcript and the side chats opened from it. The model unsubscribes when disposed.
    /// </summary>
    /// <param name="session">The session whose transcript is searched.</param>
    /// <param name="sideChats">The side chats opened from the session, or null when the session has none to search.</param>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is null.</exception>
    public TranscriptSearchModel(SessionViewModel session, SideChatPanel? sideChats)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        _sideChats = sideChats;
        _session.PropertyChanged += OnSessionPropertyChanged;
        if (_sideChats is not null)
        {
            _sideChats.PropertyChanged += OnPanelPropertyChanged;
            WatchChats();
        }
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

    /// <summary>Gets the matches of the current query in the current scope, in order.</summary>
    public IReadOnlyList<TranscriptMatch> Matches
    {
        get => _matches;
        private set
        {
            if (SetProperty(ref _matches, value))
            {
                NotifyPropertyChanged(nameof(MatchCount));
                NotifyPropertyChanged(nameof(CountLabel));
                NotifyPropertyChanged(nameof(SelectedMatch));
                NotifyPropertyChanged(nameof(SelectedBlockIndex));
            }
        }
    }

    /// <summary>Gets whether this search was given side chats, so the chat scope is on offer.</summary>
    public bool HasSideChats => _sideChats is not null;

    /// <summary>Gets the scope of the search: the parent transcript alone, or the transcript and every side chat.</summary>
    public SearchScope Scope
    {
        get => _scope;
        private set => SetProperty(ref _scope, value);
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
                NotifyPropertyChanged(nameof(SelectedMatch));
                NotifyPropertyChanged(nameof(SelectedBlockIndex));
                NotifyPropertyChanged(nameof(CountLabel));
            }
        }
    }

    /// <summary>Gets the selected match, or null when there are no matches.</summary>
    public TranscriptMatch? SelectedMatch => _selectedIndex >= 0 && _selectedIndex < _matches.Count ? _matches[_selectedIndex] : null;

    /// <summary>
    /// Gets the block index of the selected match, the one a view scrolls the parent transcript to. It is -1 when there are no
    /// matches and also when the selected match is in a side chat, which has no block in the transcript.
    /// </summary>
    public int SelectedBlockIndex => SelectedMatch is { ChatId: null } match ? match.BlockIndex : -1;

    /// <summary>Gets the identifier of the side chat holding the selected match, or null when the match is in the transcript or absent.</summary>
    public string? SelectedChatId => SelectedMatch?.ChatId;

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
        SearchAndSelectFirst();
    }

    /// <summary>
    /// Sets the scope and searches again. The first match, if any, becomes the selection. Choosing
    /// <see cref="SearchScope.TranscriptAndSideChats"/> on a search without side chats searches the transcript alone.
    /// </summary>
    /// <param name="scope">The scope to search.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> is not a defined scope.</exception>
    public void SetScope(SearchScope scope)
    {
        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope), scope, "The scope is not defined.");
        }

        if (scope == _scope)
        {
            return;
        }

        Scope = scope;
        SearchAndSelectFirst();
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

    /// <summary>Stops following the session's transcript and the side chats.</summary>
    public void Dispose()
    {
        _session.PropertyChanged -= OnSessionPropertyChanged;
        if (_sideChats is not null)
        {
            _sideChats.PropertyChanged -= OnPanelPropertyChanged;
        }

        foreach (var chat in _watchedChats)
        {
            chat.PropertyChanged -= OnChatPropertyChanged;
        }

        _watchedChats.Clear();
    }

    private IReadOnlyList<TranscriptMatch> FindMatches() =>
        _scope == SearchScope.TranscriptAndSideChats && _sideChats is not null
            ? ChatSearch.Find(_session.Tree, _sideChats.Chats, _query)
            : TranscriptSearch.Find(_session.Tree, _query);

    private void SearchAndSelectFirst()
    {
        Matches = FindMatches();
        SelectedIndex = _matches.Count > 0 ? 0 : -1;
        RaiseSelected();
    }

    private void WatchChats()
    {
        foreach (var chat in _watchedChats)
        {
            chat.PropertyChanged -= OnChatPropertyChanged;
        }

        _watchedChats.Clear();
        if (_sideChats is null)
        {
            return;
        }

        foreach (var chat in _sideChats.Chats)
        {
            chat.PropertyChanged += OnChatPropertyChanged;
            _watchedChats.Add(chat);
        }
    }

    private void OnPanelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SideChatPanel.Chats), StringComparison.Ordinal))
        {
            return;
        }

        WatchChats();
        RefreshForChatChange();
    }

    private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SideChat.Messages), StringComparison.Ordinal))
        {
            RefreshForChatChange();
        }
    }

    private void RefreshForChatChange()
    {
        if (_scope == SearchScope.TranscriptAndSideChats && _query.Length > 0)
        {
            Refresh();
        }
    }

    // Re-searches under an active query and keeps the selection on a valid match. No event is raised, so a streaming update
    // never moves the view away from the match the user chose.
    private void Refresh()
    {
        Matches = FindMatches();
        SelectedIndex = _matches.Count == 0 ? -1 : _selectedIndex < 0 ? 0 : Math.Min(_selectedIndex, _matches.Count - 1);
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SessionViewModel.Tree), StringComparison.Ordinal) || _query.Length == 0)
        {
            return;
        }

        Refresh();
    }

    private void RaiseSelected()
    {
        if (_selectedIndex >= 0)
        {
            MatchSelected?.Invoke(this, new MatchSelectedEventArgs(_matches[_selectedIndex]));
        }
    }
}

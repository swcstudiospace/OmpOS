using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.Observable;

namespace OmpOS.Native.Views.Editor;

/// <summary>
/// The state the editor view binds to: the open tabs as strip items, the selected tab's find model and the find query. It
/// wraps <see cref="EditorTabs"/> and <see cref="DocumentFindModel"/> and holds no copy of either. The models keep the
/// buffers, the matches and the selected match (ADE-09).
/// </summary>
/// <remarks>
/// The find model belongs to the selected tab, so it is recreated on every tab change. The query and the match-case flag
/// belong to the find bar, so they carry over to the find model of the next tab.
/// </remarks>
internal sealed class EditorViewModel : ObservableModel, IDisposable
{
    private readonly EditorTabs _tabs;
    private readonly ObservableCollection<EditorTabItem> _items = [];
    private DocumentFindModel? _find;
    private string _findQuery = string.Empty;
    private bool _caseSensitive;
    private bool _disposed;

    /// <summary>Creates the editor state over an existing tab set.</summary>
    /// <param name="tabs">The open tabs. The view model subscribes to them until it is disposed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="tabs"/> is null.</exception>
    public EditorViewModel(EditorTabs tabs)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        _tabs = tabs;
        Items = new ReadOnlyObservableCollection<EditorTabItem>(_items);
        _tabs.PropertyChanged += OnTabsPropertyChanged;
        ((INotifyCollectionChanged)_tabs.Tabs).CollectionChanged += OnTabsCollectionChanged;
        RebuildItems();
        BindFind();
    }

    /// <summary>Raised when the find bar moves to a match or the query changes, so the view selects that match and scrolls to it.
    /// Editing the text never raises it, so the caret stays where the user types.</summary>
    public event EventHandler? MatchRevealRequested;

    /// <summary>Gets the open tabs as the strip draws them, in the order they were opened.</summary>
    public ReadOnlyObservableCollection<EditorTabItem> Items { get; }

    /// <summary>Gets the selected tab, or null when no tab is open.</summary>
    public EditorTab? SelectedTab => _tabs.SelectedTab;

    /// <summary>Gets the find model of the selected tab, or null when no tab is open.</summary>
    public DocumentFindModel? Find => _find;

    /// <summary>Gets the selected match of the find model, or null when there are no matches.</summary>
    public TextMatch? SelectedMatch => _find?.SelectedMatch;

    /// <summary>Gets whether the query has at least one match in the selected tab.</summary>
    public bool HasMatches => _find is not null && _find.MatchCount > 0;

    /// <summary>Gets or sets the find query. Changing it selects the first match and reveals it.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string FindQuery
    {
        get => _findQuery;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _findQuery, value))
            {
                if (_find is not null)
                {
                    _find.Query = value;
                }

                NotifyPropertyChanged(nameof(MatchStatus));
                RevealSelectedMatch();
            }
        }
    }

    /// <summary>Gets or sets whether the find query respects case.</summary>
    public bool CaseSensitive
    {
        get => _caseSensitive;
        set
        {
            if (SetProperty(ref _caseSensitive, value))
            {
                if (_find is not null)
                {
                    _find.CaseSensitive = value;
                }

                NotifyPropertyChanged(nameof(MatchStatus));
                RevealSelectedMatch();
            }
        }
    }

    /// <summary>Gets the find result for the status line: "3 of 12", "No matches", or empty when there is no query.</summary>
    public string MatchStatus
    {
        get
        {
            if (_find is null || _findQuery.Length == 0)
            {
                return string.Empty;
            }

            return _find.MatchCount == 0
                ? "No matches"
                : string.Create(CultureInfo.InvariantCulture, $"{_find.SelectedIndex + 1} of {_find.MatchCount}");
        }
    }

    /// <summary>Opens a file in a tab and selects it, or selects the file's existing tab.</summary>
    /// <param name="relativePath">The workspace-relative path of the file.</param>
    /// <param name="text">The file's text. It is ignored when the file already has a tab.</param>
    public void Open(string relativePath, string text) => _tabs.Open(relativePath, text);

    /// <summary>Selects the open tab for a file.</summary>
    /// <param name="relativePath">The workspace-relative path of the file.</param>
    public void Select(string relativePath) => _tabs.Select(relativePath);

    /// <summary>Closes the tab for a file.</summary>
    /// <param name="relativePath">The workspace-relative path of the file.</param>
    /// <returns>True when a tab was closed.</returns>
    public bool Close(string relativePath) => _tabs.Close(relativePath);

    /// <summary>Moves the find selection to the next match, wrapping from the last to the first, and reveals it.</summary>
    public void NextMatch()
    {
        _find?.Next();
        RevealSelectedMatch();
    }

    /// <summary>Moves the find selection to the previous match, wrapping from the first to the last, and reveals it.</summary>
    public void PreviousMatch()
    {
        _find?.Previous();
        RevealSelectedMatch();
    }

    /// <summary>Unsubscribes from the tabs and releases the find model.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tabs.PropertyChanged -= OnTabsPropertyChanged;
        ((INotifyCollectionChanged)_tabs.Tabs).CollectionChanged -= OnTabsCollectionChanged;
        ReleaseFind();
    }

    private void OnTabsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(EditorTabs.SelectedTab), StringComparison.Ordinal))
        {
            return;
        }

        BindFind();
        UpdateItemSelection();
        NotifyPropertyChanged(nameof(SelectedTab));
    }

    private void OnTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems();

    private void OnFindPropertyChanged(object? sender, PropertyChangedEventArgs e) => NotifyFindChanged();

    private void BindFind()
    {
        ReleaseFind();

        var tab = _tabs.SelectedTab;
        if (tab is not null)
        {
            var find = new DocumentFindModel(tab.Document);
            _find = find;
            find.PropertyChanged += OnFindPropertyChanged;
            find.CaseSensitive = _caseSensitive;
            find.Query = _findQuery;
        }

        NotifyFindChanged();
    }

    private void ReleaseFind()
    {
        if (_find is null)
        {
            return;
        }

        _find.PropertyChanged -= OnFindPropertyChanged;
        _find.Dispose();
        _find = null;
    }

    private void NotifyFindChanged()
    {
        NotifyPropertyChanged(nameof(Find));
        NotifyPropertyChanged(nameof(HasMatches));
        NotifyPropertyChanged(nameof(MatchStatus));
    }

    private void RevealSelectedMatch()
    {
        if (SelectedMatch is not null)
        {
            MatchRevealRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RebuildItems()
    {
        _items.Clear();
        foreach (var tab in _tabs.Tabs)
        {
            _items.Add(new EditorTabItem(tab, ReferenceEquals(tab, _tabs.SelectedTab), this));
        }
    }

    private void UpdateItemSelection()
    {
        foreach (var item in _items)
        {
            item.IsSelected = ReferenceEquals(item.Tab, _tabs.SelectedTab);
        }
    }
}

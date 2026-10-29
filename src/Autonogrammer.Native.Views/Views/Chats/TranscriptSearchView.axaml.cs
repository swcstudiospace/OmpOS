using System.ComponentModel;
using Autonogrammer.Presentation.Chats;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Chats;

/// <summary>
/// The transcript search bar: the query box, the match count, and the Previous and Next controls. It draws the bound
/// <see cref="TranscriptSearchModel"/> and forwards typing and clicks to it. The selected match's block index is on the model
/// (<see cref="TranscriptSearchModel.SelectedBlockIndex"/>), which the session view scrolls to.
/// </summary>
/// <remarks>
/// When the model has side chats, the bar offers the chat scope as a check box. In the chat scope it also lists every match
/// with the name of the chat it is in, and names the selected match's chat; clicking a row selects that match on the model.
/// </remarks>
internal partial class TranscriptSearchView : UserControl
{
    private TranscriptSearchModel? _search;
    private IReadOnlyList<TranscriptMatch>? _renderedMatches;
    private bool _syncing;

    /// <summary>Creates the search bar with no data context.</summary>
    public TranscriptSearchView()
    {
        InitializeComponent();
        SearchInput.PropertyChanged += OnSearchInputPropertyChanged;
        PreviousButton.Click += OnPreviousClick;
        NextButton.Click += OnNextClick;
        ChatScopeToggle.IsCheckedChanged += OnChatScopeChanged;
        MatchRows.SelectionChanged += OnRowSelectionChanged;
    }

    /// <summary>Binds the search bar to the search model in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_search is not null)
        {
            _search.PropertyChanged -= OnSearchPropertyChanged;
        }

        _search = DataContext as TranscriptSearchModel;
        if (_search is not null)
        {
            _search.PropertyChanged += OnSearchPropertyChanged;
        }

        _renderedMatches = null;
        _syncing = true;
        try
        {
            SearchInput.Text = _search?.Query ?? string.Empty;
            ChatScopeToggle.IsChecked = _search?.Scope == SearchScope.TranscriptAndSideChats;
        }
        finally
        {
            _syncing = false;
        }

        RenderResults();
    }

    private void OnSearchInputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || _syncing || _search is null)
        {
            return;
        }

        _search.SetQuery(SearchInput.Text ?? string.Empty);
    }

    private void OnPreviousClick(object? sender, RoutedEventArgs e) => _search?.Previous();

    private void OnNextClick(object? sender, RoutedEventArgs e) => _search?.Next();

    private void OnChatScopeChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncing || _search is null)
        {
            return;
        }

        _search.SetScope(ChatScopeToggle.IsChecked == true ? SearchScope.TranscriptAndSideChats : SearchScope.Transcript);
    }

    private void OnRowSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _search is null || MatchRows.SelectedIndex < 0 || MatchRows.SelectedIndex >= _search.MatchCount)
        {
            return;
        }

        _search.Select(MatchRows.SelectedIndex);
    }

    private void OnSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The model may change off the UI thread; the controls are drawn only on the thread that owns them.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnSearchPropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName is nameof(TranscriptSearchModel.CountLabel)
            or nameof(TranscriptSearchModel.MatchCount)
            or nameof(TranscriptSearchModel.SelectedIndex)
            or nameof(TranscriptSearchModel.Scope))
        {
            RenderResults();
        }
    }

    private void RenderResults()
    {
        var count = _search?.MatchCount ?? 0;
        SearchCount.Text = _search?.CountLabel ?? string.Empty;
        PreviousButton.IsEnabled = count > 0;
        NextButton.IsEnabled = count > 0;

        _syncing = true;
        try
        {
            var chatScope = _search?.Scope == SearchScope.TranscriptAndSideChats;
            ChatScopeToggle.IsVisible = _search?.HasSideChats == true;
            ChatScopeToggle.IsChecked = chatScope;
            MatchRows.IsVisible = chatScope && count > 0;
            SelectedLocation.IsVisible = chatScope && count > 0;
            SelectedLocation.Text = chatScope ? _search?.SelectedMatch?.Label ?? string.Empty : string.Empty;
            RenderRows(chatScope);
        }
        finally
        {
            _syncing = false;
        }
    }

    // The rows are rebuilt only when the matches are a new list; a selection change only moves the highlighted row.
    private void RenderRows(bool chatScope)
    {
        var matches = chatScope ? _search?.Matches : null;
        if (!ReferenceEquals(matches, _renderedMatches))
        {
            _renderedMatches = matches;
            MatchRows.ItemsSource = matches?.Select(static match => match.Label).ToList();
        }

        MatchRows.SelectedIndex = chatScope ? _search?.SelectedIndex ?? -1 : -1;
    }
}

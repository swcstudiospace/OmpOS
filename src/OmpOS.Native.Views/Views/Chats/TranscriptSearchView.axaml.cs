using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmpOS.Presentation.Chats;

namespace OmpOS.Native.Views.Chats;

/// <summary>
/// The transcript search bar: the query box, the match count, and the Previous and Next controls. It draws the bound
/// <see cref="TranscriptSearchModel"/> and forwards typing and clicks to it. The selected match's block index is on the model
/// (<see cref="TranscriptSearchModel.SelectedBlockIndex"/>), which the session view scrolls to.
/// </summary>
internal partial class TranscriptSearchView : UserControl
{
    private TranscriptSearchModel? _search;
    private bool _syncing;

    /// <summary>Creates the search bar with no data context.</summary>
    public TranscriptSearchView()
    {
        InitializeComponent();
        SearchInput.PropertyChanged += OnSearchInputPropertyChanged;
        PreviousButton.Click += OnPreviousClick;
        NextButton.Click += OnNextClick;
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

        _syncing = true;
        try
        {
            SearchInput.Text = _search?.Query ?? string.Empty;
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

    private void OnSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The model may change off the UI thread; the controls are drawn only on the thread that owns them.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnSearchPropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName is nameof(TranscriptSearchModel.CountLabel) or nameof(TranscriptSearchModel.MatchCount))
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
    }
}

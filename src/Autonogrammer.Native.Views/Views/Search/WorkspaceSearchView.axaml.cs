using System.Globalization;
using Autonogrammer.Workspace;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Autonogrammer.Native.Views.Search;

/// <summary>
/// The workspace search box: the pattern, the regular-expression and ignore-case options, the search button, the error for
/// an invalid pattern, and one row per hit showing its file, line and match. Enter in the pattern box and the button both run
/// the search. It draws the bound <see cref="WorkspaceSearchModel"/>.
/// </summary>
internal partial class WorkspaceSearchView : UserControl
{
    private WorkspaceSearchModel? _model;

    /// <summary>Creates the search box with no data context.</summary>
    public WorkspaceSearchView()
    {
        InitializeComponent();
        PatternInput.KeyDown += OnPatternKeyDown;
        SearchButton.Click += OnSearchClick;
    }

    /// <summary>Binds the search box to the model in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_model is not null)
        {
            _model.Changed -= OnModelChanged;
        }

        _model = DataContext as WorkspaceSearchModel;
        if (_model is not null)
        {
            _model.Changed += OnModelChanged;
        }

        Render();
    }

    private void OnPatternKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        RunSearch();
    }

    private void OnSearchClick(object? sender, RoutedEventArgs e) => RunSearch();

    private void OnModelChanged(object? sender, EventArgs e)
    {
        // A search may finish on a thread-pool continuation; the controls are drawn only on the UI thread.
        if (Dispatcher.UIThread.CheckAccess())
        {
            Render();
        }
        else
        {
            Dispatcher.UIThread.Post(() => Render());
        }
    }

    private void RunSearch()
    {
        if (_model is null)
        {
            return;
        }

        // SearchAsync publishes its outcome through the model; it never throws for a bad pattern, so the task is not awaited here.
        var search = _model.SearchAsync(
            PatternInput.Text ?? string.Empty,
            RegexCheck.IsChecked == true,
            IgnoreCaseCheck.IsChecked == true,
            CancellationToken.None);
        _ = search;
    }

    private void Render()
    {
        var model = _model;
        IReadOnlyList<SearchHit> hits = model?.Hits ?? [];
        if (!ReferenceEquals(HitRows.ItemsSource, hits))
        {
            HitRows.ItemsSource = hits;
        }

        var error = model?.Error;
        SearchError.Text = error ?? string.Empty;
        SearchError.IsVisible = error is not null;
        SearchStatus.Text = StatusFor(model);
    }

    private static string StatusFor(WorkspaceSearchModel? model)
    {
        if (model is null || !model.HasSearched)
        {
            return string.Empty;
        }

        if (model.IsSearching)
        {
            return "Searching…";
        }

        if (model.Error is not null)
        {
            return string.Empty;
        }

        if (model.Hits.Count == 0)
        {
            return "No matches.";
        }

        var count = model.Hits.Count.ToString(CultureInfo.CurrentCulture) + (model.Truncated ? "+" : string.Empty);
        return count + " matches" + (model.Truncated ? "; the list stops at the hit limit." : ".");
    }
}

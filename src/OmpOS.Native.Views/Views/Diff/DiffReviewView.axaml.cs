using System.ComponentModel;
using Avalonia.Controls;
using OmpOS.Presentation.Editor;

namespace OmpOS.Native.Views.Diff;

/// <summary>
/// The per-hunk review of one file's diff. It draws each hunk of the bound <see cref="DiffReview"/> with its own Accept and
/// Reject controls, which decide only that hunk in the model. It also shows the resulting file text, so the user sees what a
/// decision changes before anything is written.
/// </summary>
/// <remarks>
/// The view holds no file: the orchestrator reads the review's <see cref="DiffReview.Apply"/> result when it writes.
/// </remarks>
internal partial class DiffReviewView : UserControl
{
    private DiffReview? _review;
    private IReadOnlyList<DiffHunkRow> _rows = [];

    /// <summary>Creates the view with no review bound.</summary>
    public DiffReviewView()
    {
        InitializeComponent();
    }

    /// <summary>Binds the view to the review in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        Unbind();
        var review = DataContext as DiffReview;
        _review = review;
        if (review is null)
        {
            _rows = [];
            HunkRows.ItemsSource = null;
            PendingText.Text = string.Empty;
            ResultBox.Text = string.Empty;
            return;
        }

        review.PropertyChanged += OnReviewChanged;
        foreach (var hunk in review.Hunks)
        {
            hunk.PropertyChanged += OnHunkChanged;
        }

        _rows = Enumerable.Range(0, review.Hunks.Count).Select(index => new DiffHunkRow(review, index)).ToArray();
        HunkRows.ItemsSource = _rows;
        Refresh();
    }

    private void Unbind()
    {
        if (_review is null)
        {
            return;
        }

        _review.PropertyChanged -= OnReviewChanged;
        foreach (var hunk in _review.Hunks)
        {
            hunk.PropertyChanged -= OnHunkChanged;
        }

        _review = null;
    }

    private void OnReviewChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    private void OnHunkChanged(object? sender, PropertyChangedEventArgs e) => Refresh();

    // Redraws the pending count, the decision on each row and the resulting text from the model's current state.
    private void Refresh()
    {
        if (_review is null)
        {
            return;
        }

        PendingText.Text = $"{_review.PendingCount} of {_review.Hunks.Count} hunks pending";
        ResultBox.Text = _review.Apply();
        foreach (var row in _rows)
        {
            row.NotifyDecisionChanged();
        }
    }
}

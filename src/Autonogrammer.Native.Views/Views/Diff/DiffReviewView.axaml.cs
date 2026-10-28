using System.ComponentModel;
using Avalonia.Controls;
using Autonogrammer.Presentation.Changes;
using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Native.Views.Diff;

/// <summary>
/// The per-hunk review of one file's diff. It draws each hunk of the bound <see cref="DiffReview"/> with its own Accept and
/// Reject controls, which decide only that hunk in the model. It also shows the resulting file text, so the user sees what a
/// decision changes before anything is written.
/// </summary>
/// <remarks>
/// The view holds no file: the orchestrator reads the review's <see cref="DiffReview.Apply"/> result when it writes. While no review is
/// bound it says <see cref="ChangeCopy.NothingToReview"/>. A review of an agent's changes also names its file with <see cref="ReviewPath"/>,
/// which shows the Apply review control; the control raises <see cref="ApplyRequested"/> and the owner writes the decisions.
/// </remarks>
internal partial class DiffReviewView : UserControl
{
    private DiffReview? _review;
    private IReadOnlyList<DiffHunkRow> _rows = [];
    private string? _reviewPath;

    /// <summary>Creates the view with no review bound.</summary>
    public DiffReviewView()
    {
        InitializeComponent();
        ApplyButton.Click += (_, _) => ApplyRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Occurs when the user asks to write the decisions of a review of an agent's changes to the file.</summary>
    public event EventHandler? ApplyRequested;

    /// <summary>
    /// Gets or sets the workspace-relative path of the file the bound review is of, or null when the review is not of an agent's changes.
    /// A path names the file in the header and shows the Apply review control.
    /// </summary>
    public string? ReviewPath
    {
        get => _reviewPath;
        set
        {
            _reviewPath = value;
            RefreshChrome();
        }
    }

    /// <summary>Shows the result of the last write of the review, or clears the line when the text is empty. The line is a polite live region.</summary>
    /// <param name="text">The sentence to show.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public void ShowResult(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        ResultText.Text = text;
        ResultText.IsVisible = text.Length > 0;
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
            RefreshChrome();
            return;
        }

        review.PropertyChanged += OnReviewChanged;
        foreach (var hunk in review.Hunks)
        {
            hunk.PropertyChanged += OnHunkChanged;
        }

        _rows = Enumerable.Range(0, review.Hunks.Count).Select(index => new DiffHunkRow(review, index)).ToArray();
        HunkRows.ItemsSource = _rows;
        RefreshChrome();
        Refresh();
    }

    // The empty sentence while no review is bound; otherwise the review, with the header of an agent's changes when the file is named.
    private void RefreshChrome()
    {
        var hasReview = _review is not null;
        EmptyText.IsVisible = !hasReview;
        ReviewContent.IsVisible = hasReview;
        var named = hasReview && _reviewPath is not null;
        ReviewHeader.IsVisible = named;
        TitleText.Text = hasReview && _reviewPath is { } path ? ChangeCopy.ReviewTitle(path) : string.Empty;
        if (!named)
        {
            ShowResult(string.Empty);
        }
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

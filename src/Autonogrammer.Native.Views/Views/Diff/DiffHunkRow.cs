using System.Windows.Input;
using Autonogrammer.Native.Views.Editor;
using Autonogrammer.Presentation.Editor;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Native.Views.Diff;

/// <summary>
/// One hunk row of the diff review. It reads its hunk's decision from the <see cref="DiffReview"/> and decides only that hunk
/// through <see cref="AcceptCommand"/> and <see cref="RejectCommand"/>.
/// </summary>
internal sealed class DiffHunkRow : ObservableModel
{
    private readonly DiffReview _review;

    /// <summary>Creates the row for one hunk of the review.</summary>
    /// <param name="review">The review that owns the hunk.</param>
    /// <param name="index">The zero-based index of the hunk in the review.</param>
    public DiffHunkRow(DiffReview review, int index)
    {
        _review = review;
        Index = index;

        var hunk = review.Hunks[index].Hunk;
        Heading = $"Hunk {index + 1} of {review.Hunks.Count}, starting at original line {hunk.OldStart}";
        Lines = hunk.Lines.Select(line => new DiffLineRow(line)).ToArray();

        AcceptCommand = new EditorCommand(() => review.Decide(index, HunkDecision.Accept));
        RejectCommand = new EditorCommand(() => review.Decide(index, HunkDecision.Reject));
    }

    /// <summary>Gets the zero-based index of the hunk in the review.</summary>
    public int Index { get; }

    /// <summary>Gets the heading that names the hunk and its place in the diff.</summary>
    public string Heading { get; }

    /// <summary>Gets the accessible name of the Accept control, which names the hunk by its one-based number.</summary>
    public string AcceptName => $"Accept hunk {Index + 1}";

    /// <summary>Gets the accessible name of the Reject control, which names the hunk by its one-based number.</summary>
    public string RejectName => $"Reject hunk {Index + 1}";

    /// <summary>Gets the lines of the hunk in diff order.</summary>
    public IReadOnlyList<DiffLineRow> Lines { get; }

    /// <summary>Gets the command that accepts this hunk.</summary>
    public ICommand AcceptCommand { get; }

    /// <summary>Gets the command that rejects this hunk.</summary>
    public ICommand RejectCommand { get; }

    /// <summary>Gets the decision as the row states it.</summary>
    public string DecisionText => _review.Hunks[Index].Decision switch
    {
        HunkDecision.Accept => "Accepted",
        HunkDecision.Reject => "Rejected",
        _ => "Pending",
    };

    /// <summary>Announces that the hunk's decision may have changed, so the row reads it again.</summary>
    public void NotifyDecisionChanged() => NotifyPropertyChanged(nameof(DecisionText));
}

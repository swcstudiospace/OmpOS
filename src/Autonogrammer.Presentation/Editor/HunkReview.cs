using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Editor;

/// <summary>One hunk of a <see cref="DiffReview"/> with its decision. A new review starts every hunk as <see cref="HunkDecision.Pending"/>.</summary>
public sealed class HunkReview : ObservableModel
{
    private HunkDecision _decision = HunkDecision.Pending;

    internal HunkReview(int index, DiffHunk hunk)
    {
        Index = index;
        Hunk = hunk;
    }

    /// <summary>Gets the zero-based position of the hunk in the diff.</summary>
    public int Index { get; }

    /// <summary>Gets the parsed hunk.</summary>
    public DiffHunk Hunk { get; }

    /// <summary>Gets the decision. It changes only through <see cref="DiffReview.Decide"/>.</summary>
    public HunkDecision Decision
    {
        get => _decision;
        internal set => SetProperty(ref _decision, value);
    }
}

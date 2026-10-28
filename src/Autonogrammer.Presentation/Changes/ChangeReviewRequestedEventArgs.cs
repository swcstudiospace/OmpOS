using Autonogrammer.Presentation.Editor;

namespace Autonogrammer.Presentation.Changes;

/// <summary>Raised when the user asks to review one changed file: the review to show, with every hunk of the agent's edits accepted.</summary>
public sealed class ChangeReviewRequestedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="path">The workspace-relative path of the file.</param>
    /// <param name="review">The review of the agent's edits to the file.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="review"/> is null.</exception>
    public ChangeReviewRequestedEventArgs(string path, DiffReview review)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(review);

        Path = path;
        Review = review;
    }

    /// <summary>Gets the workspace-relative path of the file under review.</summary>
    public string Path { get; }

    /// <summary>
    /// Gets the review. Its original is the file's text before the agent's first edit and every hunk starts accepted, so rejecting a hunk
    /// reverts it. Hand it back to <see cref="ChangeSetModel.ApplyReview"/> to write the decisions.
    /// </summary>
    public DiffReview Review { get; }
}

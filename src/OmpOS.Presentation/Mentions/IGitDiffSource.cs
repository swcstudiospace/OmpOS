namespace OmpOS.Presentation.Mentions;

/// <summary>The git diffs a mention can attach. It is the seam over a git client so Presentation runs no git.</summary>
public interface IGitDiffSource
{
    /// <summary>Summarises a diff for the picker row.</summary>
    /// <param name="kind">Which diff.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>Whether the diff is available and how many files it changes.</returns>
    Task<MentionGitDiffSummary> GetSummaryAsync(MentionGitDiffKind kind, CancellationToken cancellationToken);

    /// <summary>Reads a diff as unified diff text.</summary>
    /// <param name="kind">Which diff.</param>
    /// <param name="maxBytes">The byte cap.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The diff text.</returns>
    /// <exception cref="MentionUnavailableException">There is no repository or no base branch.</exception>
    Task<MentionContent> ReadAsync(MentionGitDiffKind kind, int maxBytes, CancellationToken cancellationToken);
}

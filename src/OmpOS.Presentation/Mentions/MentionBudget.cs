namespace OmpOS.Presentation.Mentions;

/// <summary>An estimate of how much context the attached mentions add to the next message.</summary>
/// <param name="EstimatedTokens">The estimated number of tokens.</param>
/// <param name="EstimatedBytes">The estimated number of bytes of content, after the per-mention cap.</param>
/// <param name="UnknownSizeCount">How many mentions have no known size and were counted at the assumed size.</param>
public sealed record MentionBudget(long EstimatedTokens, long EstimatedBytes, int UnknownSizeCount)
{
    /// <summary>Gets the estimate of no mentions.</summary>
    public static MentionBudget None { get; } = new(0, 0, 0);
}

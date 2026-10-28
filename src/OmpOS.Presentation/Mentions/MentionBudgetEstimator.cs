namespace OmpOS.Presentation.Mentions;

/// <summary>
/// Estimates the tokens that attached mentions add to a message. The estimate is one token per four bytes (a rule of thumb for English text and code), each
/// mention capped at the byte cap the send applies, and a mention of unknown size (a diff, terminal output) counted at an assumed size. It is an estimate, shown
/// as such; the context ring shows the real usage after the turn.
/// </summary>
public static class MentionBudgetEstimator
{
    /// <summary>The bytes counted as one token.</summary>
    public const int BytesPerToken = 4;

    /// <summary>The size assumed for a mention whose size is not known (8 KiB).</summary>
    public const int AssumedUnknownBytes = 8 * 1024;

    /// <summary>Estimates the tokens of a number of bytes, rounding up.</summary>
    /// <param name="bytes">The bytes; negative counts as zero.</param>
    /// <returns>The tokens.</returns>
    public static long TokensForBytes(long bytes) => bytes <= 0 ? 0 : (bytes + BytesPerToken - 1) / BytesPerToken;

    /// <summary>Estimates the budget of a set of mentions.</summary>
    /// <param name="mentions">The attached mentions.</param>
    /// <param name="maxContentBytes">The byte cap applied to each mention at send time.</param>
    /// <returns>The estimate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mentions"/> is null.</exception>
    public static MentionBudget Estimate(IReadOnlyList<ResolvedMention> mentions, int maxContentBytes)
    {
        ArgumentNullException.ThrowIfNull(mentions);

        long bytes = 0;
        var unknown = 0;
        foreach (var mention in mentions)
        {
            if (mention.SizeBytes < 0)
            {
                unknown++;
                bytes += Math.Min(AssumedUnknownBytes, maxContentBytes);
            }
            else
            {
                bytes += Math.Min(mention.SizeBytes, maxContentBytes);
            }
        }

        return new MentionBudget(TokensForBytes(bytes), bytes, unknown);
    }
}

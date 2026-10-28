namespace Autonogrammer.Harness.Completion;

/// <summary>
/// The result of one inline edit request: its outcome, and the proposed replacement when there is one.
/// </summary>
public sealed record InlineEditResult
{
    private InlineEditResult(InlineEditOutcome outcome, string replacement)
    {
        Outcome = outcome;
        Replacement = replacement;
    }

    /// <summary>Gets the result for a request made with no provider configured.</summary>
    public static InlineEditResult Disabled { get; } = new(InlineEditOutcome.Disabled, string.Empty);

    /// <summary>Gets the result for a provider that answered without a proposal.</summary>
    public static InlineEditResult NoProposal { get; } = new(InlineEditOutcome.NoProposal, string.Empty);

    /// <summary>Creates the result for a provider proposal.</summary>
    /// <param name="replacement">The proposed text for the selected range. It may be empty, which proposes deleting the selection.</param>
    /// <returns>A result with outcome <see cref="InlineEditOutcome.Proposed"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="replacement"/> is null.</exception>
    public static InlineEditResult Proposed(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        return new InlineEditResult(InlineEditOutcome.Proposed, replacement);
    }

    /// <summary>Gets the outcome of the request.</summary>
    public InlineEditOutcome Outcome { get; }

    /// <summary>Gets the proposed replacement. Empty unless <see cref="Outcome"/> is <see cref="InlineEditOutcome.Proposed"/>.</summary>
    public string Replacement { get; }
}

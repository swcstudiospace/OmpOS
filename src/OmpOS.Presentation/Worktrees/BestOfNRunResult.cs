namespace OmpOS.Presentation.Worktrees;

/// <summary>How one candidate of a best-of-n run ended.</summary>
/// <param name="Succeeded">Whether the agent finished its turn.</param>
/// <param name="Summary">What the agent said it did, or the reason it did not finish.</param>
public sealed record BestOfNRunResult(bool Succeeded, string Summary);

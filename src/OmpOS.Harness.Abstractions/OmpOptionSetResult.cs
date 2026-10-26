namespace OmpOS.Harness.Abstractions;

/// <summary>The result of an attempt to change an option of a session.</summary>
/// <param name="Outcome">How the attempt ended.</param>
/// <param name="CurrentValue">The value the option has after the attempt, or <see langword="null"/> when the engine reports none.</param>
/// <param name="Message">A plain sentence that says why the attempt did not change the option, or <see langword="null"/> after a change.</param>
public sealed record OmpOptionSetResult(OmpOptionSetOutcome Outcome, string? CurrentValue, string? Message)
{
    /// <summary>Gets a value indicating whether the engine confirmed the requested value.</summary>
    public bool IsSet => Outcome == OmpOptionSetOutcome.Set;
}
